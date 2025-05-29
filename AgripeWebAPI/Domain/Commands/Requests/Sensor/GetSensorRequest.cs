using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensor
{
    public class GetSensorRequest : IRequest<GetSensorResponse>
    {
        public string Id { get; set; }
    }
}
