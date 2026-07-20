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
    public class AdminEditUserHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_User_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Active = true };
            // first call (lookup by id) returns user; second call (email conflict) returns null
            mockUsers.SetupSequence(c => c.FindAsync(
                    It.IsAny<FilterDefinition<User>>(),
                    It.IsAny<FindOptions<User, User>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(new List<User> { user }).Object)
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(new List<User>()).Object);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminEditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object);
            var request = new AdminEditUserRequest { Id = 1, Name = "New", Email = "old@example.com", Active = true, IsAdmin = true };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal("New", result.Name);
            Assert.True(result.IsAdmin);
        }

        [Fact]
        public async Task Handle_UserNotFound_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminEditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object);
            var request = new AdminEditUserRequest { Id = 99, Name = "X", Email = "x@x.com" };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Usuário não encontrado.")), Times.Once);
        }

        [Fact]
        public async Task Handle_EmailConflict_NotifiesError()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com" };
            var conflictUser = new User { Id = 2, Name = "Other", Email = "new@example.com" };
            mockUsers.SetupSequence(c => c.FindAsync(
                    It.IsAny<FilterDefinition<User>>(),
                    It.IsAny<FindOptions<User, User>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(new List<User> { user }).Object)
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(new List<User> { conflictUser }).Object);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new AdminEditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object);
            var request = new AdminEditUserRequest { Id = 1, Name = "Old", Email = "new@example.com" };

            var result = await handler.Handle(request, default);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.Is<StarkAgroAPI.Notifications.Notification>(
                notif => notif.Mensagem == "Email já em uso por outro usuário.")), Times.Once);
        }

        [Fact]
        public async Task Handle_WithPassword_HashesPassword()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var notifier = new Mock<INotifier>();

            var user = new User { Id = 1, Name = "Old", Email = "old@example.com" };
            mockUsers.SetupSequence(c => c.FindAsync(
                    It.IsAny<FilterDefinition<User>>(),
                    It.IsAny<FindOptions<User, User>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(new List<User> { user }).Object)
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(new List<User>()).Object);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.HashPassword("NewPass123")).Returns("hashed_new");

            var handler = new AdminEditUserHandler(mockDbContext.Object, passwordHasher.Object, notifier.Object);
            var request = new AdminEditUserRequest { Id = 1, Name = "Old", Email = "old@example.com", Password = "NewPass123" };

            await handler.Handle(request, default);

            passwordHasher.Verify(p => p.HashPassword("NewPass123"), Times.Once);
        }
    }
}
