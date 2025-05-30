using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class GetListSensorByUserIdRequest : IRequest<IList<GetSensorResponse>>
    {
        public int UserId { get; set; }
    }
}
