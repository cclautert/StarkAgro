using StarkAgroAPI.Domain.Commands.Responses.Reads;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Reads
{
    public class CreateDeviceReadRequest : IRequest<CreateReadResponse?>
    {
        public string Code { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsEdgeAnomaly { get; set; }
        public EdgeStats? EdgeStats { get; set; }
    }
}
