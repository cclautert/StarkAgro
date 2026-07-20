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
    public class EditUserHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_User_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<EditUserHandler>>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Password = "oldpass" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns<string>(p => "hashed_" + p);

            var handler = new EditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new EditUserRequest { Name = "New", Email = "old@example.com", Password = string.Empty };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal("New", result.Name);
            Assert.Equal("old@example.com", result.Email);
        }

        [Fact]
        public async Task Handle_DifferentUserId_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<EditUserHandler>>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Password = "oldpass" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new EditUserRequest { Name = "New", Email = "old@example.com", Password = string.Empty, CurrentUserId = 999 };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Você não tem permissão para editar este usuário.")), Times.Once);
        }

        [Fact]
        public async Task Handle_WithPasswordChange_HashesPassword()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<EditUserHandler>>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Password = "oldpass" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns<string>(p => "hashed_" + p);

            var handler = new EditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new EditUserRequest { Name = "Old", Email = "old@example.com", Password = "Test@123" };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            passwordHasher.Verify(p => p.HashPassword("Test@123"), Times.Once);
        }

        [Fact]
        public async Task Handle_WithoutPassword_KeepsExisting()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<EditUserHandler>>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Password = "oldpass" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new EditUserRequest { Name = "New", Email = "old@example.com", Password = string.Empty };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            passwordHasher.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UserNotFound_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<EditUserHandler>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new EditUserRequest { Name = "New", Email = "notfound@example.com" };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Email não encontrado.")), Times.Once);
        }

        [Fact]
        public async Task Handle_WeakPassword_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<EditUserHandler>>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Password = "oldpass" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            // "weak" doesn't meet PasswordStrength requirements (needs uppercase, digit, special char, min length)
            var request = new EditUserRequest { Name = "Old", Email = "old@example.com", Password = "weak" };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "A senha não atende aos requisitos de segurança.")), Times.Once);
        }

        [Fact]
        public async Task Handle_ReplaceOneThrows_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<EditUserHandler>>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Password = "oldpass" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DB error"));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object, logger.Object);
            var request = new EditUserRequest { Name = "New", Email = "old@example.com", Password = string.Empty };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Erro ao atualizar usuário. Tente novamente.")), Times.Once);
        }
    }
}
