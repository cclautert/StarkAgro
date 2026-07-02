using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MediatR;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class RegisterWebPushSubscriptionHandlerTests
    {
        private static string Sub(string endpoint) =>
            $"{{\"endpoint\":\"{endpoint}\",\"keys\":{{\"p256dh\":\"abc\",\"auth\":\"xyz\"}}}}";

        private static (Mock<agpDBContext>, Mock<IMongoCollection<User>>) BuildMocks(User? user = null)
        {
            var db = new Mock<agpDBContext>();
            var users = new Mock<IMongoCollection<User>>();
            db.Setup(d => d.Users).Returns(users.Object);
            MongoMockHelper.SetupFind(users, user);
            users.Setup(u => u.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            return (db, users);
        }

        [Fact]
        public async Task Handle_UserExists_AddsSubscriptionToList()
        {
            var existingUser = new User { Id = 1 };
            var (db, users) = BuildMocks(existingUser);
            var handler = new RegisterWebPushSubscriptionHandler(db.Object);
            var json = Sub("https://fcm.googleapis.com/abc") + "  ";
            var request = new RegisterWebPushSubscriptionRequest { UserId = 1, SubscriptionJson = json };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            Assert.Single(existingUser.WebPushSubscriptions);
            Assert.Equal(json.Trim(), existingUser.WebPushSubscriptions[0]);
            users.Verify(u => u.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_SameEndpoint_ReplacesExistingEntry()
        {
            var existingUser = new User
            {
                Id = 1,
                WebPushSubscriptions = new List<string> { Sub("https://web.push.apple.com/dev1"), Sub("https://fcm.googleapis.com/old") }
            };
            var (db, _) = BuildMocks(existingUser);
            var handler = new RegisterWebPushSubscriptionHandler(db.Object);
            var renewed = $"{{\"endpoint\":\"https://fcm.googleapis.com/old\",\"keys\":{{\"p256dh\":\"NEW\",\"auth\":\"NEW\"}}}}";

            await handler.Handle(new RegisterWebPushSubscriptionRequest { UserId = 1, SubscriptionJson = renewed }, CancellationToken.None);

            Assert.Equal(2, existingUser.WebPushSubscriptions.Count);
            Assert.Contains(renewed, existingUser.WebPushSubscriptions);
            Assert.DoesNotContain(Sub("https://fcm.googleapis.com/old"), existingUser.WebPushSubscriptions);
            Assert.Contains(Sub("https://web.push.apple.com/dev1"), existingUser.WebPushSubscriptions);
        }

        [Fact]
        public async Task Handle_LegacyField_MigratedIntoList()
        {
            var existingUser = new User { Id = 1, WebPushSubscriptionJson = Sub("https://fcm.googleapis.com/legacy") };
            var (db, _) = BuildMocks(existingUser);
            var handler = new RegisterWebPushSubscriptionHandler(db.Object);

            await handler.Handle(new RegisterWebPushSubscriptionRequest
            {
                UserId = 1,
                SubscriptionJson = Sub("https://web.push.apple.com/iphone")
            }, CancellationToken.None);

            Assert.Null(existingUser.WebPushSubscriptionJson);
            Assert.Equal(2, existingUser.WebPushSubscriptions.Count);
            Assert.Contains(Sub("https://fcm.googleapis.com/legacy"), existingUser.WebPushSubscriptions);
            Assert.Contains(Sub("https://web.push.apple.com/iphone"), existingUser.WebPushSubscriptions);
        }

        [Fact]
        public async Task Handle_MoreThanFiveSubscriptions_KeepsMostRecentFive()
        {
            var existingUser = new User
            {
                Id = 1,
                WebPushSubscriptions = Enumerable.Range(1, 5).Select(i => Sub($"https://push.example.com/dev{i}")).ToList()
            };
            var (db, _) = BuildMocks(existingUser);
            var handler = new RegisterWebPushSubscriptionHandler(db.Object);

            await handler.Handle(new RegisterWebPushSubscriptionRequest
            {
                UserId = 1,
                SubscriptionJson = Sub("https://push.example.com/dev6")
            }, CancellationToken.None);

            Assert.Equal(5, existingUser.WebPushSubscriptions.Count);
            Assert.DoesNotContain(Sub("https://push.example.com/dev1"), existingUser.WebPushSubscriptions);
            Assert.Contains(Sub("https://push.example.com/dev6"), existingUser.WebPushSubscriptions);
        }

        [Fact]
        public async Task Handle_UserNotFound_ThrowsKeyNotFoundException()
        {
            var (db, _) = BuildMocks(null);
            var handler = new RegisterWebPushSubscriptionHandler(db.Object);
            var request = new RegisterWebPushSubscriptionRequest { UserId = 99, SubscriptionJson = "{}" };

            await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(request, CancellationToken.None));
        }

        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RegisterWebPushSubscriptionHandler(null!));
        }
    }
}
