using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class CreateSensorHandler : IRequestHandler<CreateSensorRequest, CreateSensorResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreateSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public Task<CreateSensorResponse> Handle(CreateSensorRequest request, CancellationToken cancellationToken)
        {
            if (!request.UserId.HasValue)
            {
                throw new ArgumentNullException(nameof(request.UserId), "UserId cannot be null.");
            }
            if (request?.Pivot?.Id <= 0)
            {
                throw new ArgumentNullException(nameof(request.Pivot.Id), "PivoId cannot be null.");
            }
            var sensor = new Models.Entities.Sensor
            {
                PivoId = request.Pivot.Id,
                UserId = request.UserId.Value,
                Code = request.Code,
                Quadrante = request.Quadrante
            };

            _dbContext.Sensors.Add(sensor);
            _dbContext.SaveChanges();

            return Task.FromResult(new CreateSensorResponse { Id = sensor.Id });
        }
    }
}
