using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class DeleteSensorHandler : IRequestHandler<DeleteSensorRequest, DeleteSensorResponse>
    {
        private readonly agpDBContext _dbContext;
        public DeleteSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<DeleteSensorResponse> Handle(DeleteSensorRequest request, CancellationToken cancellationToken)
        {
            var result = await _dbContext.Sensors.DeleteOneAsync(s => s.Id == request.Id, cancellationToken);
            if (result.DeletedCount == 0)
            {
                throw new KeyNotFoundException($"Sensor with ID {request.Id} not found.");
            }

            await _dbContext.ReadSensors.DeleteManyAsync(r => r.SensorId == request.Id, cancellationToken);
            return new DeleteSensorResponse { Success = true };
        }
    }
}
