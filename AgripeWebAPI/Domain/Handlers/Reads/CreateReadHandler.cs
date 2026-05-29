using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Reads
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

            var read = new ReadSensor
            {
                Id = await _dbContext.GetNextIdAsync(nameof(ReadSensor), cancellationToken),
                SensorId = sensor.Id,
                UserId = sensor.UserId,
                Value = request.Value,
                Date = DateTime.UtcNow
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
