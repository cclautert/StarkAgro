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
    public class RegisterExpoPushTokenHandlerTests
    {
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
        public async Task Handle_UserExists_SetsTokenAndReplacesDocument()
        {
            var existingUser = new User { Id = 1 };
            var (db, users) = BuildMocks(existingUser);
            var handler = new RegisterExpoPushTokenHandler(db.Object);
            var request = new RegisterExpoPushTokenRequest { UserId = 1, Token = "ExponentPushToken[abc123]  " };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            Assert.Equal("ExponentPushToken[abc123]", existingUser.ExpoPushToken);
            users.Verify(u => u.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.Is<User>(u => u.ExpoPushToken == "ExponentPushToken[abc123]"),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ThrowsKeyNotFoundException()
        {
            var (db, _) = BuildMocks(null);
            var handler = new RegisterExpoPushTokenHandler(db.Object);
            var request = new RegisterExpoPushTokenRequest { UserId = 99, Token = "token" };

            await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(request, CancellationToken.None));
        }

        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RegisterExpoPushTokenHandler(null!));
        }
    }
}
