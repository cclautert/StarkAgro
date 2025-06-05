using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class UserTokenRequest : IRequest<UserTokenResponse>
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
