using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Notifications;
using System.Collections.Generic;

namespace AgripeWebAPI.Tests.Mocks
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