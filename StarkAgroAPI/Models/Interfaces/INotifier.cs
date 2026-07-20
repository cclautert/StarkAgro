using StarkAgroAPI.Notifications;
using MediatR;

namespace StarkAgroAPI.Models.Interfaces
{
    public interface INotifier
    {
        bool HasNotification();
        List<Notification> getNotifications();
        void Handle(Notification notification);
    }
}
