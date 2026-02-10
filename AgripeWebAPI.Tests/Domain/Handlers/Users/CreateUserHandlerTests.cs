using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            var options = new DbContextOptionsBuilder<agpDBContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_CreateUserHandler")
                .Options;
            var context = new agpDBContext(options);

            var passwordHasher = new Mock<IPasswordHasher>();
            passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns<string>(p => "hashed_" + p);
            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<CreateUserHandler>>();

            var handler = new CreateUserHandler(context, passwordHasher.Object, notifier.Object, logger.Object);
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