using AgripeWebAPI.Domain.Commands.Responses.Reads;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Reads
{
    public class CreateLoRaWanReadRequest : IRequest<CreateReadResponse?>
    {
        public string Code { get; set; } = string.Empty;
        public decimal? Humidity { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? BatteryVoltage { get; set; }
        public DateTime? ReadAt { get; set; }
        public int? Fcnt { get; set; }
    }
}
