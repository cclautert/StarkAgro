using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Sensors
{
    public class GetListSensorRequest : IRequest<IList<GetSensorResponse>>
    {
        public int PivotId { get; set; }
        public int Quadrante { get; set; }
        public int? UserId { get; set; }
    }
}
