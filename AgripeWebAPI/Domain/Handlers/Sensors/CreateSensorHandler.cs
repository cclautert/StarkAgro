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
            var sensor = new Models.Entities.Sensor
            {
                PivoId = request.PivoId,
                UserId = request.UserId,
                Code = request.Code,
                Quadrante = request.Quadrante
            };

            _dbContext.Sensors.Add(sensor);
            _dbContext.SaveChanges();

            return Task.FromResult(new CreateSensorResponse { Id = sensor.Id });
        }
    }
}
