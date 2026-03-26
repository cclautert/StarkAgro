using AgripeWebAPI.Notifications;

namespace AgripeWebAPI.Tests.Notifications
{
    public class NotificatorTests
    {
        [Fact]
        public void HasNotification_Empty_ReturnsFalse()
        {
            // Arrange
            var notificator = new Notificator();

            // Act
            var result = notificator.HasNotification();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasNotification_WithItems_ReturnsTrue()
        {
            // Arrange
            var notificator = new Notificator();
            notificator.Handle(new Notification("Some error"));

            // Act
            var result = notificator.HasNotification();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Handle_AddsNotification()
        {
            // Arrange
            var notificator = new Notificator();
            var notification = new Notification("Test message");

            // Act
            notificator.Handle(notification);

            // Assert
            Assert.Single(notificator.getNotifications());
            Assert.Same(notification, notificator.getNotifications()[0]);
        }

        [Fact]
        public void GetNotifications_ReturnsAll()
        {
            // Arrange
            var notificator = new Notificator();
            var n1 = new Notification("First");
            var n2 = new Notification("Second");
            var n3 = new Notification("Third");

            notificator.Handle(n1);
            notificator.Handle(n2);
            notificator.Handle(n3);

            // Act
            var result = notificator.getNotifications();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Contains(n1, result);
            Assert.Contains(n2, result);
            Assert.Contains(n3, result);
        }
    }
}
