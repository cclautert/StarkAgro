using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Admin
{
    public class AdminToggleUserActiveHandlerTests
    {
        [Fact]
        public async Task Handle_Deactivates_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();

            var user = new User { Id = 1, Name = "Alice", Email = "alice@example.com", Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<User>>(),
                    It.IsAny<UpdateDefinition<User>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminToggleUserActiveHandler(mockDbContext.Object, notifier.Object);
            var result = await handler.Handle(new AdminToggleUserActiveRequest { Id = 1, Active = false }, default);

            Assert.NotNull(result);
            Assert.False(result.Active);
        }

        [Fact]
        public async Task Handle_UserNotFound_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminToggleUserActiveHandler(mockDbContext.Object, notifier.Object);
            var result = await handler.Handle(new AdminToggleUserActiveRequest { Id = 99, Active = false }, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Usuário não encontrado.")), Times.Once);
        }
    }
}
