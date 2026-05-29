using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Anomalies
{
    public class DetectSensorAnomalyRequest : IRequest<Unit>
    {
        public int ReadSensorId { get; set; }
        public int SensorId { get; set; }
        public int UserId { get; set; }
        public decimal Value { get; set; }
    }
}
