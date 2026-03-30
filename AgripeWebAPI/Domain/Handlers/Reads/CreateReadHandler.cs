using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Reads
{
    public class CreateReadHandler : IRequestHandler<CreateReadRequest, CreateReadResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreateReadHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));            
        }

        public async Task<CreateReadResponse> Handle(CreateReadRequest request, CancellationToken cancellationToken)
        {
            var sensor = await _dbContext.Sensors.Find(s => s.Code == request.Code).FirstOrDefaultAsync(cancellationToken);
            if (sensor == null)
            {
                throw new KeyNotFoundException($"Sensor with code '{request.Code}' not found.");
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

            return new CreateReadResponse();
        }
    }
}
