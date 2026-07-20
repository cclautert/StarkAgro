using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Sensors
{
    public class GetListSensorByUserIdRequest : IRequest<IList<GetSensorResponse>>
    {
        public int? UserId { get; set; }
    }
}
