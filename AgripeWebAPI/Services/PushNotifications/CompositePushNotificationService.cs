using AgripeWebAPI.Models.Interfaces;

namespace AgripeWebAPI.Services.PushNotifications
{
    public class CompositePushNotificationService : IPushNotificationService
    {
        private readonly ExpoPushNotificationService _expo;
        private readonly WebPushNotificationService _web;

        public CompositePushNotificationService(
            ExpoPushNotificationService expo,
            WebPushNotificationService web)
        {
            _expo = expo ?? throw new ArgumentNullException(nameof(expo));
            _web = web ?? throw new ArgumentNullException(nameof(web));
        }

        public Task SendAsync(int userId, string title, string body, CancellationToken cancellationToken = default)
            => Task.WhenAll(
                _expo.SendAsync(userId, title, body, cancellationToken),
                _web.SendAsync(userId, title, body, cancellationToken));
    }
}
