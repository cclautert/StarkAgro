using StarkAgroAPI.Domain.Commands.Responses.Irrigation;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Irrigation
{
    public class AcceptRejectProposalRequest : IRequest<AcceptRejectProposalResponse?>
    {
        public int ProposalId { get; set; }
        public string Action { get; set; } = string.Empty; // "accept" | "reject"
    }
}
