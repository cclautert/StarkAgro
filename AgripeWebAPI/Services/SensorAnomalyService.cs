using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MongoDB.Driver;

namespace AgripeWebAPI.Services
{
    public class SensorAnomalyService : ISensorAnomalyService
    {
        private const int MinSamples = 10;
        private const double Threshold = 2.5;

        private readonly agpDBContext _dbContext;
        private readonly ILogger<SensorAnomalyService> _logger;

        public SensorAnomalyService(agpDBContext dbContext, ILogger<SensorAnomalyService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> DetectAndSaveAsync(
            ReadSensor reading,
            int pivotId,
            IReadOnlyList<ReadSensor> lastNReadings,
            CancellationToken cancellationToken = default)
        {
            if (lastNReadings.Count < MinSamples)
                return false;

            var values = lastNReadings.Select(r => (double)r.Value).ToList();
            var mean = values.Average();
            var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
            var stddev = Math.Sqrt(variance);

            // Flat distribution — skip detection
            if (stddev < 0.001)
                return false;

            var lowerBound = mean - Threshold * stddev;
            var upperBound = mean + Threshold * stddev;
            var currentValue = (double)reading.Value;

            if (currentValue >= lowerBound && currentValue <= upperBound)
                return false;

            var updateFilter = Builders<ReadSensor>.Filter.Eq(r => r.Id, reading.Id);
            var updateDef = Builders<ReadSensor>.Update.Set(r => r.IsAnomaly, true);
            await _dbContext.ReadSensors.UpdateOneAsync(updateFilter, updateDef, cancellationToken: cancellationToken);

            var anomaly = new SensorAnomaly
            {
                Id = await _dbContext.GetNextIdAsync(nameof(SensorAnomaly), cancellationToken),
                SensorId = reading.SensorId,
                PivotId = pivotId,
                UserId = reading.UserId,
                ReadSensorId = reading.Id,
                Value = reading.Value,
                ExpectedMin = (decimal)Math.Round(lowerBound, 4),
                ExpectedMax = (decimal)Math.Round(upperBound, 4),
                Date = DateTime.UtcNow,
                Acknowledged = false
            };

            await _dbContext.SensorAnomalies.InsertOneAsync(anomaly, cancellationToken: cancellationToken);

            _logger.LogWarning(
                "Anomaly detected for sensor {SensorId} (pivot {PivotId}, user {UserId}): value {Value} outside [{Min:F4}, {Max:F4}]",
                reading.SensorId, pivotId, reading.UserId, reading.Value, lowerBound, upperBound);

            return true;
        }
    }
}
