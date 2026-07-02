using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;
using System.Text.Json;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class RegisterWebPushSubscriptionHandler : IRequestHandler<RegisterWebPushSubscriptionRequest, Unit>
    {
        private const int MaxSubscriptionsPerUser = 5;

        private readonly agpDBContext _dbContext;

        public RegisterWebPushSubscriptionHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<Unit> Handle(RegisterWebPushSubscriptionRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Find(u => u.Id == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
                throw new KeyNotFoundException($"User {request.UserId} not found");

            var newSubscription = request.SubscriptionJson.Trim();
            var newEndpoint = TryGetEndpoint(newSubscription);

            user.WebPushSubscriptions ??= new List<string>();

            // Migrate the legacy single-subscription field into the list
            if (!string.IsNullOrWhiteSpace(user.WebPushSubscriptionJson))
            {
                user.WebPushSubscriptions.Add(user.WebPushSubscriptionJson);
                user.WebPushSubscriptionJson = null;
            }

            // One entry per device: replace any subscription with the same endpoint
            if (newEndpoint is not null)
                user.WebPushSubscriptions.RemoveAll(s => TryGetEndpoint(s) == newEndpoint);

            user.WebPushSubscriptions.Add(newSubscription);

            if (user.WebPushSubscriptions.Count > MaxSubscriptionsPerUser)
                user.WebPushSubscriptions = user.WebPushSubscriptions
                    .Skip(user.WebPushSubscriptions.Count - MaxSubscriptionsPerUser)
                    .ToList();

            await _dbContext.Users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: cancellationToken);

            return Unit.Value;
        }

        private static string? TryGetEndpoint(string subscriptionJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(subscriptionJson);
                return doc.RootElement.TryGetProperty("endpoint", out var ep) ? ep.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
