using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class MarkAlertsReadHandlerTests
    {
        private static (Mock<agpDBContext> db, Mock<IMongoCollection<User>> users, Mock<ICurrentUserContext> currentUser) BuildMocks(int? userId = 1)
        {
            var db = new Mock<agpDBContext>();
            var users = new Mock<IMongoCollection<User>>();
            users.Setup(u => u.UpdateOneAsync(
                    It.IsAny<FilterDefinition<User>>(),
                    It.IsAny<UpdateDefinition<User>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            db.Setup(d => d.Users).Returns(users.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(userId);

            return (db, users, currentUser);
        }

        [Fact]
        public async Task Handle_AuthenticatedUser_UpdatesAlertsReadAt()
        {
            var (db, users, currentUser) = BuildMocks();
            var handler = new MarkAlertsReadHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new MarkAlertsReadRequest(), CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            users.Verify(u => u.UpdateOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<UpdateDefinition<User>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NoAuthenticatedUser_ThrowsInvalidOperationException()
        {
            var (db, _, currentUser) = BuildMocks(userId: null);
            var handler = new MarkAlertsReadHandler(db.Object, currentUser.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(new MarkAlertsReadRequest(), CancellationToken.None));
        }

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            var (db, _, currentUser) = BuildMocks();
            Assert.Throws<ArgumentNullException>(() => new MarkAlertsReadHandler(null!, currentUser.Object));
            Assert.Throws<ArgumentNullException>(() => new MarkAlertsReadHandler(db.Object, null!));
        }
    }
}
