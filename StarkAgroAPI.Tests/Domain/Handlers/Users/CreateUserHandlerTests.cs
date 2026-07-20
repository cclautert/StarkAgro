using StarkAgroAPI.Domain.Commands.Requests.Users;
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
    public class CreateUserHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_User_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<CreateUserHandler>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null); // No existing user
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("User", It.IsAny<CancellationToken>())).ReturnsAsync(1);
            mockUsers.Setup(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns<string>(p => "hashed_" + p);

            var handler = new CreateUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new CreateUserRequest { Name = "Test", Email = "test@example.com", Password = "pass" };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
            Assert.Equal("test@example.com", result.Email);
        }

        [Fact]
        public async Task Handle_DuplicateEmail_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<CreateUserHandler>>();

            var existingUser = new User { Id = 1, Name = "Existing", Email = "test@example.com", Password = "p" };
            MongoMockHelper.SetupFind(mockUsers, existingUser);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new CreateUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new CreateUserRequest { Name = "New", Email = "test@example.com", Password = "pass" };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Email já cadastrado.")), Times.Once);
        }

        [Fact]
        public async Task Handle_InsertThrows_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<CreateUserHandler>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("User", It.IsAny<CancellationToken>())).ReturnsAsync(1);
            passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");
            mockUsers.Setup(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DB error"));

            var handler = new CreateUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new CreateUserRequest { Name = "Test", Email = "test@example.com", Password = "pass" };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Erro ao criar usuário. Tente novamente.")), Times.Once);
        }
    }
}
