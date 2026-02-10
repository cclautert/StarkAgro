using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class ExternalLoginRequest : IRequest<UserTokenResponse?>
    {
        public string Provider { get; set; } = string.Empty; // "Google"
        public string Code { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
    }
}
