using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Admin
{
    public class AdminCreateUserHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_User_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<AdminCreateUserHandler>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("User", It.IsAny<CancellationToken>())).ReturnsAsync(10);
            mockUsers.Setup(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");

            var handler = new AdminCreateUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new AdminCreateUserRequest { Name = "Admin", Email = "admin@example.com", Password = "Senha@123", IsAdmin = true };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal("Admin", result.Name);
            Assert.True(result.IsAdmin);
        }

        [Fact]
        public async Task Handle_DuplicateEmail_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<AdminCreateUserHandler>>();

            MongoMockHelper.SetupFind(mockUsers, new User { Id = 1, Email = "admin@example.com" });
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminCreateUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new AdminCreateUserRequest { Name = "Admin", Email = "admin@example.com", Password = "Senha@123" };

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
            var logger = new Mock<ILogger<AdminCreateUserHandler>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("User", It.IsAny<CancellationToken>())).ReturnsAsync(10);
            passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");
            mockUsers.Setup(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DB error"));

            var handler = new AdminCreateUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new AdminCreateUserRequest { Name = "Admin", Email = "admin@example.com", Password = "Senha@123" };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Erro ao criar usuário.")), Times.Once);
        }
    }
}
