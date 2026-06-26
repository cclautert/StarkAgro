using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
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
        private readonly IPushNotificationService _pushService;

        public DetectSensorAnomalyHandler(
            agpDBContext dbContext,
            ISensorAnomalyService anomalyService,
            IPushNotificationService pushService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _anomalyService = anomalyService ?? throw new ArgumentNullException(nameof(anomalyService));
            _pushService = pushService ?? throw new ArgumentNullException(nameof(pushService));
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
    }
}
