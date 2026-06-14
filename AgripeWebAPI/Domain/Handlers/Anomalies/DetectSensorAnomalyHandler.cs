using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Services;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Anomalies
{
    public class DetectSensorAnomalyHandler : IRequestHandler<DetectSensorAnomalyRequest, Unit>
    {
        private const int WindowSize = 50;

        private readonly agpDBContext _dbContext;
        private readonly ISensorAnomalyService _anomalyService;

        public DetectSensorAnomalyHandler(agpDBContext dbContext, ISensorAnomalyService anomalyService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _anomalyService = anomalyService ?? throw new ArgumentNullException(nameof(anomalyService));
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

            var reading = new ReadSensor
            {
                Id = request.ReadSensorId,
                SensorId = request.SensorId,
                UserId = request.UserId,
                Humidity = request.Humidity
            };

            await _anomalyService.DetectAndSaveAsync(reading, sensor.PivoId, baselineReadings, cancellationToken);

            return Unit.Value;
        }
    }
}
