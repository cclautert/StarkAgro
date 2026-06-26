using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class RegisterExpoPushTokenRequest : IRequest<Unit>
    {
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
    }
}
