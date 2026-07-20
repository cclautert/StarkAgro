using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class GetIrrigationTrendRequest : IRequest<IrrigationTrendResponse?>
    {
        public int? PivotId { get; set; }
        public int NumberOfReads { get; set; } = 10;
        public int UserId { get; set; }
    }
}
