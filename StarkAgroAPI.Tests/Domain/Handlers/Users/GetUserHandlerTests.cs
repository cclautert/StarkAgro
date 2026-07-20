using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Commands.Responses.Users;
using StarkAgroAPI.Domain.Handlers.Users;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Users
{
    public class GetUserHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_User_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<GetUserHandler>>();

            var expected = new GetUserResponse { Id = 2, Name = "User2", Email = "user2@example.com" };
            MongoMockHelper.SetupFindProjection<User, GetUserResponse>(mockUsers, new List<GetUserResponse> { expected });
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new GetUserHandler(mockDbContext.Object, notifier.Object, logger.Object);
            var result = await handler.Handle(new GetUserRequest { Id = 2 }, default);

            Assert.NotNull(result);
            Assert.Equal(2, result!.Id);
            Assert.Equal("User2", result.Name);
            Assert.Equal("user2@example.com", result.Email);
        }

        [Fact]
        public async Task Handle_DifferentUserId_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<GetUserHandler>>();

            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new GetUserHandler(mockDbContext.Object, notifier.Object, logger.Object);
            var result = await handler.Handle(new GetUserRequest { Id = 2, CurrentUserId = 999 }, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Você não tem permissão para acessar este usuário.")), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<GetUserHandler>>();

            MongoMockHelper.SetupFindProjection<User, GetUserResponse>(mockUsers, new List<GetUserResponse>());
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new GetUserHandler(mockDbContext.Object, notifier.Object, logger.Object);
            var result = await handler.Handle(new GetUserRequest { Id = 2 }, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Usuário não encontrado.")), Times.Once);
        }
    }
}
