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
    public class EditUserHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_User_And_Returns_Response()
        {
            var user = new User { Id = 1, Name = "Old", Email = "old@example.com", Password = "oldpass" };
            var users = new List<User> { user }.AsQueryable();
            var mockSet = new Mock<DbSet<User>>();
            mockSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
            mockSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
            mockSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
            mockSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());
            mockSet.Setup(m => m.Update(It.IsAny<User>())).Callback<User>(u =>
            {
                user.Name = u.Name;
                user.Email = u.Email;
                user.Password = u.Password;
            }).Returns((User u) => null);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Users).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var handler = new EditUserHandler(mockContext.Object);
            var request = new EditUserRequest { Name = "New", Email = "new@example.com", Password = "newpass" };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal("New", result.Name);
            Assert.Equal("new@example.com", result.Email);
        }
    }
}