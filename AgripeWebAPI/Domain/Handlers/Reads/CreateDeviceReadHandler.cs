using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Reads
{
    public class CreateDeviceReadHandler : IRequestHandler<CreateDeviceReadRequest, CreateReadResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ILogger<CreateDeviceReadHandler> _logger;

        public CreateDeviceReadHandler(agpDBContext dbContext, ILogger<CreateDeviceReadHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CreateReadResponse?> Handle(CreateDeviceReadRequest request, CancellationToken cancellationToken)
        {
            var sensor = await _dbContext.Sensors
                .Find(s => s.Code.ToUpper() == request.Code.ToUpper())
                .FirstOrDefaultAsync(cancellationToken);

            if (sensor == null)
            {
                _logger.LogWarning("Device read rejected: no sensor registered with code '{Code}'", request.Code);
                return null;
            }

            var read = new ReadSensor
            {
                Id = await _dbContext.GetNextIdAsync(nameof(ReadSensor), cancellationToken),
                SensorId = sensor.Id,
                UserId = sensor.UserId,
                Value = request.Value,
                Date = request.ReadAt ?? DateTime.UtcNow,
                IsEdgeAnomaly = request.IsEdgeAnomaly
            };

            await _dbContext.ReadSensors.InsertOneAsync(read, cancellationToken: cancellationToken);

            return new CreateReadResponse
            {
                Id = read.Id,
                SensorId = sensor.Id,
                UserId = sensor.UserId
            };
        }
    }
}
