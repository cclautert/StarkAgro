using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Anomalies
{
    public class DetectSensorAnomalyRequest : IRequest<Unit>
    {
        public int ReadSensorId { get; set; }
        public int SensorId { get; set; }
        public int UserId { get; set; }
        public decimal? Humidity { get; set; }
    }
}
