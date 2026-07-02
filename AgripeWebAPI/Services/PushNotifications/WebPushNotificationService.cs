using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebPush;

namespace AgripeWebAPI.Services.PushNotifications
{
    [ExcludeFromCodeCoverage]
    public class WebPushNotificationService
    {
        private readonly agpDBContext _dbContext;
        private readonly VapidSettings _vapid;
        private readonly ILogger<WebPushNotificationService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public WebPushNotificationService(
            agpDBContext dbContext,
            IOptions<VapidSettings> vapidSettings,
            ILogger<WebPushNotificationService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _vapid = vapidSettings?.Value ?? throw new ArgumentNullException(nameof(vapidSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendAsync(int userId, string title, string body, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
                return;

            // All device subscriptions: list + legacy single-subscription field
            var subscriptions = new List<string>(user.WebPushSubscriptions ?? new List<string>());
            if (!string.IsNullOrWhiteSpace(user.WebPushSubscriptionJson))
                subscriptions.Add(user.WebPushSubscriptionJson);

            if (subscriptions.Count == 0)
                return;

            if (_vapid.PublicKey == "CHANGE_ME" || _vapid.PrivateKey == "CHANGE_ME")
            {
                _logger.LogWarning("VAPID keys not configured — web push skipped for user {UserId}", userId);
                return;
            }

            // ngsw-worker only displays pushes wrapped in a "notification" object
            var payload = JsonSerializer.Serialize(new
            {
                notification = new
                {
                    title,
                    body,
                    icon = "assets/icons/icon-192x192.png"
                }
            });

            var vapidDetails = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);
            var client = new WebPushClient();
            var deadEndpoints = new List<string>();

            foreach (var subscriptionJson in subscriptions)
            {
                WebPushSubscriptionDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<WebPushSubscriptionDto>(subscriptionJson, _jsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize web push subscription for user {UserId}", userId);
                    continue;
                }

                if (dto is null || string.IsNullOrWhiteSpace(dto.Endpoint) ||
                    string.IsNullOrWhiteSpace(dto.Keys?.P256DH) || string.IsNullOrWhiteSpace(dto.Keys?.Auth))
                    continue;

                try
                {
                    var subscription = new PushSubscription(dto.Endpoint, dto.Keys.P256DH, dto.Keys.Auth);
                    await Task.Run(() => client.SendNotification(subscription, payload, vapidDetails), cancellationToken);
                }
                catch (WebPushException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Web push endpoint expired for user {UserId} — removing subscription", userId);
                    deadEndpoints.Add(dto.Endpoint);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Web push failed for user {UserId}", userId);
                }
            }

            if (deadEndpoints.Count > 0)
                await RemoveDeadSubscriptionsAsync(user, deadEndpoints, cancellationToken);
        }

        private async Task RemoveDeadSubscriptionsAsync(Models.Entities.User user, List<string> deadEndpoints, CancellationToken cancellationToken)
        {
            bool ContainsDeadEndpoint(string subscriptionJson) =>
                deadEndpoints.Any(endpoint => subscriptionJson.Contains(endpoint, StringComparison.OrdinalIgnoreCase));

            user.WebPushSubscriptions?.RemoveAll(ContainsDeadEndpoint);
            if (!string.IsNullOrWhiteSpace(user.WebPushSubscriptionJson) && ContainsDeadEndpoint(user.WebPushSubscriptionJson))
                user.WebPushSubscriptionJson = null;

            try
            {
                await _dbContext.Users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to prune dead web push subscriptions for user {UserId}", user.Id);
            }
        }

        private record WebPushSubscriptionDto(
            string Endpoint,
            [property: JsonPropertyName("keys")] WebPushKeysDto? Keys);

        private record WebPushKeysDto(
            [property: JsonPropertyName("p256dh")] string P256DH,
            [property: JsonPropertyName("auth")] string Auth);
    }
}
