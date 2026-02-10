using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class DeleteUserHandlerTests
    {
        [Fact]
        public async Task Handle_Deletes_User_And_Returns_Response()
        {
            // Arrange
            var user = new User { Id = 5, Name = "ToDelete", Email = "del@example.com" };
            var users = new List<User> { user }.AsQueryable();

            var mockSet = new Mock<DbSet<User>>();
            mockSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
            mockSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
            mockSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
            mockSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());
            mockSet.Setup(m => m.Remove(It.IsAny<User>())).Callback<User>(u => { });

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Users).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<DeleteUserHandler>>();

            var handler = new DeleteUserHandler(mockContext.Object, notifier.Object, logger.Object);
            var request = new DeleteUserRequest { Id = 5 };

            // Act
            var result = await handler.Handle(request, default);

            // Assert
            mockSet.Verify(m => m.Remove(It.Is<User>(u => u.Id == 5)), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Handle_Returns_Failure_When_User_Not_Found()
        {
            // Arrange
            var users = new List<User>().AsQueryable();
            var mockSet = new Mock<DbSet<User>>();
            mockSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
            mockSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
            mockSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
            mockSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Users).Returns(mockSet.Object);

            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<DeleteUserHandler>>();

            var handler = new DeleteUserHandler(mockContext.Object, notifier.Object, logger.Object);
            var request = new DeleteUserRequest { Id = 99 };

            // Act
            var result = await handler.Handle(request, default);

            // Assert
            Assert.False(result.Success);
            notifier.Verify(n => n.Handle(It.IsAny<AgripeWebAPI.Notifications.Notification>()), Times.Once);
        }
    }
}