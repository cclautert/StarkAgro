using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class GetPivotAIInsightsRequest : IRequest<PivotAIInsightsResponse?>
    {
        public int PivotId { get; set; }
        public int UserId { get; set; }
    }
}
