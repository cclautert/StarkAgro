using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Sensors
{
    public class SendSensorDownlinkRequest : IRequest<SendSensorDownlinkResponse>
    {
        public int SensorId { get; set; }
    }
}
