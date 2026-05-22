using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class GetPivotAIInsightsRequest : IRequest<PivotAIInsightsResponse?>
    {
        public int PivotId { get; set; }
        public int UserId { get; set; }
    }
}
