using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class DeleteUserRequest : IRequest<DeleteUserResponse>
    {
        public int Id { get; set; }
        public int CurrentUserId { get; set; } // Set by controller from JWT claims
    }
}
