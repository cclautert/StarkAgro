using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Notifications;
using System.Collections.Generic;

namespace StarkAgroAPI.Tests.Mocks
{
    public class MockNotifier : INotifier
    {
        private readonly List<Notification> _notifications = new();

        public void Handle(Notification notification)
        {
            _notifications.Add(notification);
        }

        public List<Notification> getNotifications()
        {
            return _notifications;
        }

        public bool HasNotification()
        {
            return _notifications.Count > 0;
        }
    }
}