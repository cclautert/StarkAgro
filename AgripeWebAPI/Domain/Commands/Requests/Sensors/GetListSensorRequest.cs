using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class GetListSensorRequest : IRequest<IList<GetSensorResponse>>
    {
        public int PivotId { get; set; }
        public int Quadrante { get; set; }
    }
}
