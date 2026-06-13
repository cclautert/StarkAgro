using AgripeWebAPI.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Globalization;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class GetIrrigationTrendHandler : IRequestHandler<GetIrrigationTrendRequest, IrrigationTrendResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly IWeatherForecastService _forecastService;
        private readonly INotifier _notifier;
        private readonly WeatherForecastSettings _settings;
        private readonly ILogger<GetIrrigationTrendHandler> _logger;

        public GetIrrigationTrendHandler(
            agpDBContext dbContext,
            IWeatherForecastService forecastService,
            INotifier notifier,
            IOptions<WeatherForecastSettings> settings,
            ILogger<GetIrrigationTrendHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IrrigationTrendResponse?> Handle(GetIrrigationTrendRequest request, CancellationToken cancellationToken)
        {
            if (request.PivotId is null || request.PivotId <= 0)
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

            var user = await _dbContext.Users
                .Find(u => u.Id == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            var limiteInferior = pivot.LimiteInferior ?? user?.LimiteInferior ?? 25m;
            var limiteSuperior = pivot.LimiteSuperior ?? user?.LimiteSuperior ?? 75m;
            var rainThreshold = pivot.RainThresholdMm ?? user?.RainThresholdMm ?? _settings.RainThresholdMm;

            var sensorFilter = Builders<Sensor>.Filter.And(
                Builders<Sensor>.Filter.Eq(s => s.PivoId, pivot.Id),
                Builders<Sensor>.Filter.Eq(s => s.UserId, request.UserId)
            );
            var sensors = await _dbContext.Sensors
                .Find(sensorFilter)
                .ToListAsync(cancellationToken);

            decimal? currentAverage = null;
            if (sensors.Count > 0)
            {
                var sensorIds = sensors.Select(s => s.Id).ToList();
                var latestPerSensor = new List<decimal>();
                foreach (var sensorId in sensorIds)
                {
                    var notAnomalyFilter = Builders<ReadSensor>.Filter.And(
                        Builders<ReadSensor>.Filter.Eq(r => r.SensorId, sensorId),
                        Builders<ReadSensor>.Filter.Ne(r => r.IsAnomaly, true));
                    var latest = await _dbContext.ReadSensors
                        .Find(notAnomalyFilter)
                        .SortByDescending(r => r.Date)
                        .Limit(1)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (latest is not null) latestPerSensor.Add(latest.Value);
                }
                if (latestPerSensor.Count > 0)
                {
                    currentAverage = Math.Round(latestPerSensor.Average(), 2);
                }
            }

            var needsIrrigation = currentAverage.HasValue && currentAverage.Value < limiteInferior;

            var response = new IrrigationTrendResponse
            {
                PivotId = pivot.Id,
                PivotName = pivot.Name,
                Latitude = pivot.Latitude,
                Longitude = pivot.Longitude,
                LimiteInferior = limiteInferior,
                LimiteSuperior = limiteSuperior,
                CurrentAverage = currentAverage,
                NeedsIrrigation = needsIrrigation,
                IrrigationPostponed = false,
                PostponeReason = null,
                WeatherForecast = null
            };

            if (!needsIrrigation || pivot.Latitude is null || pivot.Longitude is null)
            {
                return response;
            }

            var horizon = Math.Max(1, _settings.ForecastHorizonDays);
            var forecast = await _forecastService.GetForecastAsync(
                pivot.Latitude.Value,
                pivot.Longitude.Value,
                horizon,
                cancellationToken);

            response.WeatherForecast = forecast;

            if (forecast.IsAvailable && forecast.TotalPrecipitationMm >= rainThreshold)
            {
                response.IrrigationPostponed = true;
                response.PostponeReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} mm de chuva prevista nos próximos {1} dias ({2})",
                    forecast.TotalPrecipitationMm,
                    horizon,
                    forecast.Source);

                _logger.LogInformation(
                    "Irrigation postponed for pivot {PivotId} (user {UserId}) — {Mm:0.0} mm forecast via {Source}",
                    pivot.Id, request.UserId, forecast.TotalPrecipitationMm, forecast.Source);
            }
            else
            {
                _logger.LogInformation(
                    "Irrigation recommendation kept for pivot {PivotId} (user {UserId}) — forecast {Source} returned {Mm:0.0} mm (available={Available})",
                    pivot.Id, request.UserId, forecast.Source, forecast.TotalPrecipitationMm, forecast.IsAvailable);
            }

            return response;
        }
    }
}
