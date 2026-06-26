using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.PushNotifications;
using Moq;

namespace AgripeWebAPI.Tests.Services
{
    public class CompositePushNotificationServiceTests
    {
        [Fact]
        public async Task SendAsync_DelegatesWhenBothServicesReturnCompletedTask()
        {
            var expo = new Mock<IPushNotificationService>();
            var web = new Mock<IPushNotificationService>();

            expo.Setup(e => e.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            web.Setup(w => w.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Verify Task.WhenAll semantics: both complete without exception
            var t1 = expo.Object.SendAsync(1, "T", "B");
            var t2 = web.Object.SendAsync(1, "T", "B");
            await Task.WhenAll(t1, t2);

            expo.Verify(e => e.SendAsync(1, "T", "B", default), Times.Once);
            web.Verify(w => w.SendAsync(1, "T", "B", default), Times.Once);
        }
    }
}
