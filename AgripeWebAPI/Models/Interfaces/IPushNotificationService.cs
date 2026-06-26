namespace AgripeWebAPI.Models.Interfaces
{
    public interface IPushNotificationService
    {
        Task SendAsync(int userId, string title, string body, CancellationToken cancellationToken = default);
    }
}
