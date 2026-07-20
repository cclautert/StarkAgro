using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Users
{
    public class RegisterWebPushSubscriptionRequest : IRequest<Unit>
    {
        public int UserId { get; set; }
        public string SubscriptionJson { get; set; } = string.Empty;
    }
}
