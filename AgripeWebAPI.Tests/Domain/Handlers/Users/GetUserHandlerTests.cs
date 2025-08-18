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
    public class GetUserHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_User_Response()
        {
            var user = new User { Id = 2, Name = "User2", Email = "user2@example.com", Password = "pass" };
            var users = new List<User> { user }.AsQueryable();
            var mockSet = new Mock<DbSet<User>>();
            mockSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
            mockSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
            mockSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
            mockSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Users).Returns(mockSet.Object);

            var handler = new GetUserHandler(mockContext.Object);
            var request = new GetUserRequest { Id = 1 };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(2, result.Id);
            Assert.Equal("User2", result.Name);
            Assert.Equal("user2@example.com", result.Email);
        }
    }
}