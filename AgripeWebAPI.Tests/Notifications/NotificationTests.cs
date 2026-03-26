using AgripeWebAPI.Notifications;

namespace AgripeWebAPI.Tests.Notifications
{
    public class NotificationTests
    {
        [Fact]
        public void Constructor_SetsMessage()
        {
            // Arrange
            var message = "Validation failed";

            // Act
            var notification = new Notification(message);

            // Assert
            Assert.Equal(message, notification.Mensagem);
        }

        [Fact]
        public void Mensagem_ReturnsNull_WhenConstructedWithNull()
        {
            // Arrange & Act
            var notification = new Notification(null!);

            // Assert
            Assert.Null(notification.Mensagem);
        }
    }
}
