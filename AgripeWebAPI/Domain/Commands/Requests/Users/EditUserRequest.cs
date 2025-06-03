using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class EditUserRequest : IRequest<EditUserResponse>
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
