using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using MongoDB.Driver;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace AgripeWebAPI.Services.PushNotifications
{
    [ExcludeFromCodeCoverage]
    public class ExpoPushNotificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly agpDBContext _dbContext;
        private readonly ILogger<ExpoPushNotificationService> _logger;

        public ExpoPushNotificationService(
            IHttpClientFactory httpClientFactory,
            agpDBContext dbContext,
            ILogger<ExpoPushNotificationService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendAsync(int userId, string title, string body, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(user?.ExpoPushToken))
                return;

            var payload = JsonSerializer.Serialize(new
            {
                to = user.ExpoPushToken,
                title,
                body
            });

            try
            {
                var client = _httpClientFactory.CreateClient("expo_push");
                using var request = new HttpRequestMessage(HttpMethod.Post, "--/api/v2/push/send");
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Expo push returned {StatusCode} for user {UserId}: {Body}",
                        (int)response.StatusCode, userId, error);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Expo push failed for user {UserId}", userId);
            }
        }
    }
}
