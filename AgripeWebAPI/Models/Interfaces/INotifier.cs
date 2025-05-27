using AgripeWebAPI.Notifications;
using MediatR;

namespace AgripeWebAPI.Models.Interfaces
{
    public interface INotifier
    {
        bool HasNotification();
        List<Notification> getNotifications();
        void Handle(Notification notification);
    }
}
