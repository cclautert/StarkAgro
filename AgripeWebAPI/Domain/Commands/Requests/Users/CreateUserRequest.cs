using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class CreateUserRequest : IRequest<CreateUserResponse>
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
