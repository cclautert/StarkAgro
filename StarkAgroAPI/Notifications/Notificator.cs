using StarkAgroAPI.Models.Interfaces;

namespace StarkAgroAPI.Notifications
{
    public class Notificator : INotifier
    {
        private readonly List<Notification> _notifications;

        public Notificator()
        {
            _notifications = new List<Notification>();
        }

        public bool HasNotification()
        {
            return _notifications.Any();
        }

        public List<Notification> getNotifications()
        {
            return _notifications;
        }

        public void Handle(Notification notificacao)
        {
            _notifications.Add(notificacao);
        }
    }
}
