using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Anomalies
{
    public class DetectSensorAnomalyHandler : IRequestHandler<DetectSensorAnomalyRequest, Unit>
    {
        private const int WindowSize = 50;
        private const int MinSamples = 10;
        private const double Threshold = 2.5;

        private readonly agpDBContext _dbContext;
        private readonly ILogger<DetectSensorAnomalyHandler> _logger;

        public DetectSensorAnomalyHandler(agpDBContext dbContext, ILogger<DetectSensorAnomalyHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Unit> Handle(DetectSensorAnomalyRequest request, CancellationToken cancellationToken)
        {
            var baselineFilter = Builders<ReadSensor>.Filter.And(
                Builders<ReadSensor>.Filter.Eq(r => r.SensorId, request.SensorId),
                Builders<ReadSensor>.Filter.Ne(r => r.Id, request.ReadSensorId),
                Builders<ReadSensor>.Filter.Eq(r => r.IsAnomaly, false));

            var baselineReadings = await _dbContext.ReadSensors
                .Find(baselineFilter)
                .SortByDescending(r => r.Date)
                .Limit(WindowSize)
                .ToListAsync(cancellationToken);

            if (baselineReadings.Count < MinSamples)
                return Unit.Value;

            var values = baselineReadings.Select(r => (double)r.Value).ToList();
            var mean = values.Average();
            var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
            var stddev = Math.Sqrt(variance);

            // Flat distribution — skip detection (all readings identical)
            if (stddev < 0.001)
                return Unit.Value;

            var lowerBound = mean - Threshold * stddev;
            var upperBound = mean + Threshold * stddev;
            var currentValue = (double)request.Value;

            if (currentValue >= lowerBound && currentValue <= upperBound)
                return Unit.Value;

            var updateFilter = Builders<ReadSensor>.Filter.Eq(r => r.Id, request.ReadSensorId);
            var updateDef = Builders<ReadSensor>.Update.Set(r => r.IsAnomaly, true);
            await _dbContext.ReadSensors.UpdateOneAsync(updateFilter, updateDef, cancellationToken: cancellationToken);

            var anomaly = new SensorAnomaly
            {
                Id = await _dbContext.GetNextIdAsync(nameof(SensorAnomaly), cancellationToken),
                SensorId = request.SensorId,
                UserId = request.UserId,
                ReadSensorId = request.ReadSensorId,
                Value = request.Value,
                ExpectedMin = (decimal)Math.Round(lowerBound, 4),
                ExpectedMax = (decimal)Math.Round(upperBound, 4),
                Date = DateTime.UtcNow,
                Acknowledged = false
            };

            await _dbContext.SensorAnomalies.InsertOneAsync(anomaly, cancellationToken: cancellationToken);

            _logger.LogWarning(
                "Anomaly detected for sensor {SensorId} (user {UserId}): value {Value} outside [{Min:F2}, {Max:F2}]",
                request.SensorId, request.UserId, request.Value, lowerBound, upperBound);

            return Unit.Value;
        }
    }
}
