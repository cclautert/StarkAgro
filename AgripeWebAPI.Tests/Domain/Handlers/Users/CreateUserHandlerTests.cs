using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class CreateUserHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_User_And_Returns_Response()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<agpDBContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_CreateUserHandler")
                .Options;
            var context = new agpDBContext(options);

            var handler = new CreateUserHandler(context);
            var request = new CreateUserRequest { Name = "Test", Email = "test@example.com", Password = "pass" };

            // Act
            var result = await handler.Handle(request, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
            Assert.Equal("test@example.com", result.Email);
        }
    }
}