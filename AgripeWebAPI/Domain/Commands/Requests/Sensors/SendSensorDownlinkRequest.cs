using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class SendSensorDownlinkRequest : IRequest<SendSensorDownlinkResponse>
    {
        public int SensorId { get; set; }
    }
}
