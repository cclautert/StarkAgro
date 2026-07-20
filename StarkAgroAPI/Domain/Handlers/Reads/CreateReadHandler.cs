using StarkAgroAPI.Domain.Commands.Requests.Reads;
using StarkAgroAPI.Domain.Commands.Responses.Reads;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Reads
{
    public class CreateReadHandler : IRequestHandler<CreateReadRequest, CreateReadResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public CreateReadHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<CreateReadResponse> Handle(CreateReadRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new UnauthorizedAccessException("Authenticated user is required to submit sensor readings.");

            var sensor = await _dbContext.Sensors.Find(s => s.Code.ToUpper() == request.Code.ToUpper()).FirstOrDefaultAsync(cancellationToken);
            if (sensor == null)
            {
                throw new KeyNotFoundException($"Sensor with code '{request.Code}' not found.");
            }

            if (sensor.UserId != userId)
            {
                throw new UnauthorizedAccessException($"Sensor '{request.Code}' does not belong to the authenticated user.");
            }

            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var existing = await _dbContext.ReadSensors
                    .Find(r => r.IdempotencyKey == request.IdempotencyKey && r.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing != null)
                {
                    return new CreateReadResponse
                    {
                        Id = existing.Id,
                        SensorId = existing.SensorId,
                        UserId = existing.UserId,
                        IdempotencyKey = existing.IdempotencyKey
                    };
                }
            }

            var read = new ReadSensor
            {
                Id = await _dbContext.GetNextIdAsync(nameof(ReadSensor), cancellationToken),
                SensorId = sensor.Id,
                UserId = sensor.UserId,
                Humidity = request.Humidity ?? request.Value,
                Date = DateTime.UtcNow,
                IsEdgeAnomaly = request.IsEdgeAnomaly,
                EdgeDetectedAt = request.IsEdgeAnomaly ? DateTime.UtcNow : null,
                IdempotencyKey = string.IsNullOrEmpty(request.IdempotencyKey) ? null : request.IdempotencyKey
            };

            await _dbContext.ReadSensors.InsertOneAsync(read, cancellationToken: cancellationToken);

            return new CreateReadResponse
            {
                Id = read.Id,
                SensorId = sensor.Id,
                UserId = sensor.UserId,
                IdempotencyKey = read.IdempotencyKey
            };
        }
    }
}
