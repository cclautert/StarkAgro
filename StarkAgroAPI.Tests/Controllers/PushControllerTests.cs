using StarkAgroAPI.Configuration;
using StarkAgroAPI.Controllers;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace StarkAgroAPI.Tests.Controllers
{
    public class PushControllerTests
    {
        [Fact]
        public void GetVapidPublicKey_ReturnsPublicKey()
        {
            var notifier = new Mock<INotifier>();
            notifier.Setup(n => n.HasNotification()).Returns(false);
            notifier.Setup(n => n.getNotifications()).Returns(new List<Notification>());
            var vapidOptions = Options.Create(new VapidSettings { PublicKey = "test-public-key-abc123" });
            var controller = new PushController(notifier.Object, vapidOptions);

            var result = controller.GetVapidPublicKey();

            var ok = Assert.IsType<ObjectResult>(result.Result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public void Constructor_NullVapidSettings_ThrowsArgumentNullException()
        {
            var notifier = new Mock<INotifier>();
            Assert.Throws<ArgumentNullException>(() => new PushController(notifier.Object, null!));
        }
    }
}
