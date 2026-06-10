using AgripeWebAPI.Domain.Commands.Responses.Irrigation;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Irrigation
{
    public class AcceptRejectProposalRequest : IRequest<AcceptRejectProposalResponse?>
    {
        public int ProposalId { get; set; }
        public string Action { get; set; } = string.Empty; // "accept" | "reject"
    }
}
