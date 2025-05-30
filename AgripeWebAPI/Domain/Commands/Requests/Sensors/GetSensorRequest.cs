using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class GetSensorRequest : IRequest<GetSensorResponse>
    {
        public string Code { get; set; }
    }
}
