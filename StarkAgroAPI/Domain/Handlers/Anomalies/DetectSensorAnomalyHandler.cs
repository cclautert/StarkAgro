using StarkAgroAPI.Configuration;
using StarkAgroAPI.Domain.Commands.Requests.Anomalies;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Anomalies
{
    public class DetectSensorAnomalyHandler : IRequestHandler<DetectSensorAnomalyRequest, Unit>
    {
        private const int WindowSize = 50;

        // If the freshest non-anomalous reading is already older than this, the stream has
        // been flagging every new reading against a stale baseline for a while — none of them
        // can ever refresh it, a self-perpetuating lock. See fallback query below.
        private static readonly TimeSpan BaselineStaleAfter = TimeSpan.FromHours(24);

        private readonly agpDBContext _dbContext;
        private readonly ISensorAnomalyService _anomalyService;
        private readonly IPushNotificationService _pushService;
        private readonly IAgricultureWeatherService _weatherService;
        private readonly IMemoryCache _cache;
        private readonly WeatherForecastSettings _weatherSettings;
        private readonly ILogger<DetectSensorAnomalyHandler> _logger;

        public DetectSensorAnomalyHandler(
            agpDBContext dbContext,
            ISensorAnomalyService anomalyService,
            IPushNotificationService pushService,
            IAgricultureWeatherService weatherService,
            IMemoryCache cache,
            IOptions<WeatherForecastSettings> weatherSettings,
            ILogger<DetectSensorAnomalyHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _anomalyService = anomalyService ?? throw new ArgumentNullException(nameof(anomalyService));
            _pushService = pushService ?? throw new ArgumentNullException(nameof(pushService));
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _weatherSettings = weatherSettings?.Value ?? throw new ArgumentNullException(nameof(weatherSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Unit> Handle(DetectSensorAnomalyRequest request, CancellationToken cancellationToken)
        {
            var sensor = await _dbContext.Sensors
                .Find(s => s.Id == request.SensorId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sensor is null)
                return Unit.Value;

            var baselineFilter = Builders<ReadSensor>.Filter.And(
                Builders<ReadSensor>.Filter.Eq(r => r.SensorId, request.SensorId),
                Builders<ReadSensor>.Filter.Ne(r => r.Id, request.ReadSensorId),
                Builders<ReadSensor>.Filter.Ne(r => r.IsAnomaly, true));

            var baselineReadings = await _dbContext.ReadSensors
                .Find(baselineFilter)
                .SortByDescending(r => r.Date)
                .Limit(WindowSize)
                .ToListAsync(cancellationToken);

            // Break the self-lock: if even the freshest non-anomalous reading is stale, fall
            // back to the raw recent readings (regardless of anomaly flag) so the baseline can
            // catch up with a sustained shift instead of staying frozen forever.
            var isStale = baselineReadings.Count == 0 || DateTime.UtcNow - baselineReadings[0].Date > BaselineStaleAfter;
            if (isStale)
            {
                var rawFilter = Builders<ReadSensor>.Filter.And(
                    Builders<ReadSensor>.Filter.Eq(r => r.SensorId, request.SensorId),
                    Builders<ReadSensor>.Filter.Ne(r => r.Id, request.ReadSensorId));

                baselineReadings = await _dbContext.ReadSensors
                    .Find(rawFilter)
                    .SortByDescending(r => r.Date)
                    .Limit(WindowSize)
                    .ToListAsync(cancellationToken);
            }

            // Rain suppression: high humidity while it has been raining at the pivot's
            // location is expected (saturated soil), not an anomaly. Skipping detection also
            // lets the rainy reading join the baseline, so the expected range adapts.
            if (await IsHighReadingExplainedByRainAsync(request, sensor, baselineReadings, cancellationToken))
                return Unit.Value;

            var reading = new ReadSensor
            {
                Id = request.ReadSensorId,
                SensorId = request.SensorId,
                UserId = request.UserId,
                Humidity = request.Humidity
            };

            var isAnomaly = await _anomalyService.DetectAndSaveAsync(reading, sensor.PivoId, baselineReadings, cancellationToken);

            if (isAnomaly)
            {
                var quadrante = sensor.Quadrante.ToString();
                var code = sensor.Code ?? sensor.Id.ToString();
                await _pushService.SendAsync(
                    request.UserId,
                    "Anomalia de Sensor",
                    $"Sensor {code} — Quadrante {quadrante}: {request.Humidity:F1}% fora dos limites esperados.",
                    cancellationToken);
            }

            return Unit.Value;
        }

        private async Task<bool> IsHighReadingExplainedByRainAsync(
            DetectSensorAnomalyRequest request,
            Sensor sensor,
            IReadOnlyList<ReadSensor> baselineReadings,
            CancellationToken cancellationToken)
        {
            // Only the high side can be explained by rain — a drop while raining is even
            // more suspicious and must keep alerting.
            if (baselineReadings.Count == 0)
                return false;

            var baselineAverage = baselineReadings.Average(r => (double)(r.Humidity ?? 0));
            if ((double)(request.Humidity ?? 0) <= baselineAverage)
                return false;

            var pivot = await _dbContext.Pivots
                .Find(p => p.Id == sensor.PivoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pivot?.Latitude is null || pivot.Longitude is null)
                return false;

            var recentRainMm = await GetRecentRainCachedAsync(pivot.Latitude.Value, pivot.Longitude.Value, cancellationToken);
            if (recentRainMm is null)
                return false; // fail-open: weather unavailable, detect as usual

            var user = await _dbContext.Users
                .Find(u => u.Id == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            var rainThreshold = pivot.RainThresholdMm ?? user?.RainThresholdMm ?? _weatherSettings.RainThresholdMm;

            if (recentRainMm.Value < rainThreshold)
                return false;

            _logger.LogInformation(
                "Anomaly suppressed by rain for sensor {SensorId} (pivot {PivotId}): humidity {Humidity}% with {RainMm}mm rain in last {Days}d (threshold {Threshold}mm)",
                request.SensorId, sensor.PivoId, request.Humidity, recentRainMm.Value,
                _weatherSettings.AnomalyRainLookbackDays, rainThreshold);

            return true;
        }

        private async Task<double?> GetRecentRainCachedAsync(double latitude, double longitude, CancellationToken cancellationToken)
        {
            var lookbackDays = Math.Max(1, _weatherSettings.AnomalyRainLookbackDays);
            var cacheKey = $"recent-rain:{latitude:F3}:{longitude:F3}:{lookbackDays}";

            if (_cache.TryGetValue(cacheKey, out double cachedMm))
                return cachedMm;

            var rainMm = await _weatherService.GetRecentPrecipitationAsync(latitude, longitude, lookbackDays, cancellationToken);
            if (rainMm is null)
                return null; // do not cache failures

            _cache.Set(cacheKey, rainMm.Value, TimeSpan.FromMinutes(Math.Max(1, _weatherSettings.CacheDurationMinutes)));
            return rainMm;
        }
    }
}
