using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class CreateSensorHandler : IRequestHandler<CreateSensorRequest, CreateSensorResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public CreateSensorHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<CreateSensorResponse> Handle(CreateSensorRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to create a sensor.");
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
                UserId = userId,
                Code = request.Code,
                Quadrante = request.Quadrante,
                UplinkIntervalSeconds = request.UplinkIntervalSeconds ?? 10800
            };

            await _dbContext.Sensors.InsertOneAsync(sensor, cancellationToken: cancellationToken);

            return new CreateSensorResponse { Id = sensor.Id };
        }
    }
}
