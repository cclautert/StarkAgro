using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Revenda
{
    public class GetMyRevendaInvitesRequest : IRequest<List<RevendaInviteResponse>>
    {
    }

    public class AcceptRevendaInviteRequest : IRequest<bool>
    {
        public int InviteId { get; set; }
    }

    public class DeclineRevendaInviteRequest : IRequest<bool>
    {
        public int InviteId { get; set; }
    }
}
