using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensor
{
    public class GetListSensorRequest : IRequest<IList<GetSensorResponse>>
    {
        public string Id { get; set; }
    }
}
