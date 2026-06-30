using AgripeWebAPI.Domain.Commands.Requests.Admin;
using AgripeWebAPI.Domain.Handlers.Admin;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Admin
{
    public class AdminDeleteUserHandlerTests
    {
        [Fact]
        public async Task Handle_Deletes_User_Returns_True()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();

            var user = new User { Id = 5, Name = "Target", Email = "target@example.com" };
            MongoMockHelper.SetupFind(mockUsers, user);
            MongoMockHelper.SetupDeleteOne(mockUsers, 1);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminDeleteUserHandler(mockDbContext.Object, notifier.Object);
            var result = await handler.Handle(new AdminDeleteUserRequest { Id = 5 }, default);

            Assert.True(result);
        }

        [Fact]
        public async Task Handle_UserNotFound_NotifiesError_Returns_False()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminDeleteUserHandler(mockDbContext.Object, notifier.Object);
            var result = await handler.Handle(new AdminDeleteUserRequest { Id = 99 }, default);

            Assert.False(result);
            notifier.Verify(n => n.Handle(It.Is<AgripeWebAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Usuário não encontrado.")), Times.Once);
        }
    }
}
