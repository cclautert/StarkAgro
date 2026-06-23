using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class GetMoisturePredictionHandler : IRequestHandler<GetMoisturePredictionRequest, MoisturePredictionResponse?>
    {
        private const int HistoryWindowHours = 168;
        private const int MinHistoryHours = 24;
        private const int ProjectionHours = 72;
        // ET0 data covers the next 3 days, which matches the projection window.
        private const int EtForecastDays = 3;
        // Confidence penalty when no coordinates → no ET component.
        private const double NoCoordinatesConfidenceFactor = 0.7;

        private readonly agpDBContext _dbContext;
        private readonly IAgricultureWeatherService _agricultureWeather;
        private readonly INotifier _notifier;
        private readonly ILogger<GetMoisturePredictionHandler> _logger;

        public GetMoisturePredictionHandler(
            agpDBContext dbContext,
            IAgricultureWeatherService agricultureWeather,
            INotifier notifier,
            ILogger<GetMoisturePredictionHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _agricultureWeather = agricultureWeather ?? throw new ArgumentNullException(nameof(agricultureWeather));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MoisturePredictionResponse?> Handle(
            GetMoisturePredictionRequest request, CancellationToken cancellationToken)
        {
            if (request.PivotId <= 0)
            {
                _notifier.Handle(new Notification("PivotId is required."));
                return null;
            }

            var pivot = await _dbContext.Pivots
                .Find(p => p.Id == request.PivotId && p.UserId == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pivot is null)
            {
                _notifier.Handle(new Notification("Pivot not found."));
                return null;
            }

            var sensors = await _dbContext.Sensors
                .Find(s => s.PivoId == pivot.Id && s.UserId == request.UserId)
                .ToListAsync(cancellationToken);

            if (sensors.Count == 0)
            {
                _notifier.Handle(new Notification(
                    "Insufficient data: no sensors found for this pivot."));
                return null;
            }

            var sensorIds = sensors.Select(s => s.Id).ToList();
            var cutoff = DateTime.UtcNow.AddHours(-HistoryWindowHours);

            var readings = await _dbContext.ReadSensors
                .Find(r => sensorIds.Contains(r.SensorId) && r.Date >= cutoff && r.IsAnomaly != true)
                .SortBy(r => r.Date)
                .ToListAsync(cancellationToken);

            if (readings.Count == 0 ||
                (readings[readings.Count - 1].Date - readings[0].Date).TotalHours < MinHistoryHours)
            {
                _notifier.Handle(new Notification(
                    $"Insufficient data: at least {MinHistoryHours} hours of sensor history is required for prediction."));
                return null;
            }

            // Aggregate: average across all sensors per hour bucket.
            var hourlyAverages = readings
                .GroupBy(r => new DateTime(r.Date.Year, r.Date.Month, r.Date.Day, r.Date.Hour, 0, 0, DateTimeKind.Utc))
                .OrderBy(g => g.Key)
                .Select(g => (Hour: g.Key, Value: g.Average(r => (double)(r.Humidity ?? 0))))
                .ToList();

            if (hourlyAverages.Count < 2)
            {
                _notifier.Handle(new Notification(
                    $"Insufficient data: at least {MinHistoryHours} hours of sensor history is required for prediction."));
                return null;
            }

            var t0 = hourlyAverages[0].Hour;
            var times = hourlyAverages.Select(h => (h.Hour - t0).TotalHours).ToList();
            var values = hourlyAverages.Select(h => h.Value).ToList();

            var (histSlope, intercept, rmse, r2) = MoisturePredictionAlgorithm.FitWeightedLinear(times, values);

            // The "current" moisture is the value at the last historical point.
            double lastMoisture = intercept + histSlope * times[times.Count - 1];
            var projectionStart = hourlyAverages[hourlyAverages.Count - 1].Hour;

            // ET0 component — only when the pivot has coordinates.
            double etHourlyRate = 0;
            bool hasCoordinates = pivot.Latitude.HasValue && pivot.Longitude.HasValue;

            if (hasCoordinates)
            {
                try
                {
                    var agri = await _agricultureWeather.GetAgricultureDataAsync(
                        pivot.Latitude!.Value, pivot.Longitude!.Value, EtForecastDays, cancellationToken);

                    if (agri is not null)
                    {
                        double et0Daily = MoisturePredictionAlgorithm.ET0DailyMm(
                            agri.TempMax, agri.TempMin, agri.ShortwaveRadiationMJm2);
                        etHourlyRate = MoisturePredictionAlgorithm.ET0ToHourlyMoistureRate(et0Daily);

                        _logger.LogInformation(
                            "Pivot {PivotId}: ET0={Et0:.2f} mm/day, hourly moisture rate={Rate:.4f}%/h",
                            pivot.Id, et0Daily, etHourlyRate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ET0 data fetch failed for pivot {PivotId}; continuing without ET component", pivot.Id);
                }
            }

            var projected = MoisturePredictionAlgorithm.Project(
                lastMoisture, histSlope, etHourlyRate, rmse, projectionStart, ProjectionHours);

            var limiteInferior = (double)(pivot.LimiteInferior ?? 25m);

            DateTime? criticalAt = null;
            foreach (var (date, moisture, _, _) in projected)
            {
                if (moisture < limiteInferior)
                {
                    criticalAt = date;
                    break;
                }
            }

            double confidence = Math.Round(r2, 2);
            if (!hasCoordinates)
                confidence = Math.Round(confidence * NoCoordinatesConfidenceFactor, 2);

            var predictedValues = projected.Select(p => new PredictedMoisturePoint
            {
                Date = p.Date,
                PredictedMoisture = Math.Round(p.Moisture, 2),
                ConfidenceMin = Math.Round(p.Min, 2),
                ConfidenceMax = Math.Round(p.Max, 2)
            }).ToList();

            return new MoisturePredictionResponse
            {
                PivotId = pivot.Id,
                PredictedValues = predictedValues,
                EstimatedCriticalAt = criticalAt,
                Confidence = confidence,
                DataPointsUsed = readings.Count
            };
        }
    }
}
