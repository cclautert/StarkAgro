using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class GetSensorTelemetryRequest : IRequest<IList<SensorTelemetryResponse>>
    {
        public int PivotId { get; set; }
        public int? UserId { get; set; }
    }
}
