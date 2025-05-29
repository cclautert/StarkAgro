using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensor
{
    public class GetListSensorByUserIdRequest : IRequest<IList<GetSensorResponse>>
    {
        public int UserId { get; set; }
    }
}
