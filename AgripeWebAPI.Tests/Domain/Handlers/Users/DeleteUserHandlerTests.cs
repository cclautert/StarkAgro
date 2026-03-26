using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class DeleteUserHandlerTests
    {
        [Fact]
        public async Task Handle_Deletes_User_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<DeleteUserHandler>>();

            var user = new User { Id = 5, Name = "ToDelete", Email = "del@example.com", Password = "p" };
            MongoMockHelper.SetupFind(mockUsers, user);
            MongoMockHelper.SetupDeleteOne(mockUsers, 1);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new DeleteUserHandler(mockDbContext.Object, notifier.Object, logger.Object);
            var result = await handler.Handle(new DeleteUserRequest { Id = 5 }, default);

            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Handle_Returns_Failure_When_User_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<DeleteUserHandler>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new DeleteUserHandler(mockDbContext.Object, notifier.Object, logger.Object);
            var result = await handler.Handle(new DeleteUserRequest { Id = 99 }, default);

            Assert.False(result.Success);
            notifier.Verify(n => n.Handle(It.IsAny<AgripeWebAPI.Notifications.Notification>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DifferentUserId_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<DeleteUserHandler>>();

            var user = new User { Id = 5, Name = "Target", Email = "target@example.com", Password = "p" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new DeleteUserHandler(mockDbContext.Object, notifier.Object, logger.Object);
            var result = await handler.Handle(new DeleteUserRequest { Id = 5, CurrentUserId = 999 }, default);

            Assert.NotNull(result);
            Assert.False(result.Success);
            notifier.Verify(n => n.Handle(It.Is<AgripeWebAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Você não tem permissão para deletar este usuário.")), Times.Once);
        }

        [Fact]
        public async Task Handle_DeleteThrows_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<DeleteUserHandler>>();

            var user = new User { Id = 5, Name = "ToDelete", Email = "del@example.com", Password = "p" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<User>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DB error"));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new DeleteUserHandler(mockDbContext.Object, notifier.Object, logger.Object);
            var result = await handler.Handle(new DeleteUserRequest { Id = 5 }, default);

            Assert.False(result.Success);
            notifier.Verify(n => n.Handle(It.Is<AgripeWebAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Erro ao deletar usuário. Tente novamente.")), Times.Once);
        }
    }
}
