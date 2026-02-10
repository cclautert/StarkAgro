using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class CreateSensorHandler : IRequestHandler<CreateSensorRequest, CreateSensorResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreateSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<CreateSensorResponse> Handle(CreateSensorRequest request, CancellationToken cancellationToken)
        {
            if (!request.UserId.HasValue)
            {
                throw new ArgumentNullException(nameof(request.UserId), "UserId cannot be null.");
            }
            if (request.Pivot == null)
            {
                throw new ArgumentNullException(nameof(request.Pivot), "Pivot cannot be null.");
            }
            if (request.Pivot.Id <= 0)
            {
                throw new ArgumentNullException(nameof(request.Pivot.Id), "PivotId must be greater than zero.");
            }
            var sensor = new Sensor
            {
                Id = await _dbContext.GetNextIdAsync(nameof(Sensor), cancellationToken),
                Name = request.Name,
                PivoId = request.Pivot.Id,
                UserId = request.UserId.Value,
                Code = request.Code,
                Quadrante = request.Quadrante
            };

            await _dbContext.Sensors.InsertOneAsync(sensor, cancellationToken: cancellationToken);

            return new CreateSensorResponse { Id = sensor.Id };
        }
    }
}
