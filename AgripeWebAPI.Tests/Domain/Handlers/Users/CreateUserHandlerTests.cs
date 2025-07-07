using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class CreateUserHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_User_And_Returns_Response()
        {
            // Arrange
            var users = new List<User>().AsQueryable();
            var mockSet = new Mock<DbSet<User>>();
            mockSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
            mockSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
            mockSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
            mockSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());
            mockSet.Setup(m => m.Add(It.IsAny<User>())).Callback<User>(u => u.Id = 123);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Users).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var handler = new CreateUserHandler(mockContext.Object);
            var request = new CreateUserRequest { Name = "Test", Email = "test@example.com", Password = "pass" };

            // Act
            var result = await handler.Handle(request, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
            Assert.Equal("Test", result.Name);
            Assert.Equal("test@example.com", result.Email);
        }
    }
}