using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Sensor
{
    public class GetSensorHandler : IRequestHandler<GetSensorRequest, GetSensorResponse>
    {
        public Task<GetSensorResponse> Handle(GetSensorRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
