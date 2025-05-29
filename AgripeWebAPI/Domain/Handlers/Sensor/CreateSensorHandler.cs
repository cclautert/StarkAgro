using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Sensor
{
    public class CreateSensorHandler : IRequestHandler<CreateSensorRequest, CreateSensorResponse>
    {
        public Task<CreateSensorResponse> Handle(CreateSensorRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
