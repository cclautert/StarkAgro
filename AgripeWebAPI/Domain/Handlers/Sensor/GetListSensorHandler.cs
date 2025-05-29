using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Sensor
{
    public class GetListSensorHandler : IRequestHandler<GetListSensorRequest, IList<GetSensorResponse>>
    {
        public Task<IList<GetSensorResponse>> Handle(GetListSensorRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
