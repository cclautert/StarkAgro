using StarkAgroAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Users
{
    public class GetUserRequest : IRequest<GetUserResponse>
    {
        public int Id { get; set; }
        public int CurrentUserId { get; set; } // Set by controller from JWT claims
    }
}
