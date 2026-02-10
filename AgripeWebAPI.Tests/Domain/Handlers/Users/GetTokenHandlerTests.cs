using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class GetTokenHandlerTests
    {
        private agpDBContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<agpDBContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_TokenHandler" + System.Guid.NewGuid())
                .Options;
            return new agpDBContext(options);
        }

        [Fact]
        public async Task Handle_Returns_Token_For_Valid_User()
        {
            // Arrange
            var context = CreateInMemoryContext();
            context.Users.Add(new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "hashed-pass", Active = true });
            context.SaveChanges();

            var passwordHasher = new Mock<IPasswordHasher>();
            passwordHasher.Setup(p => p.VerifyPassword("pass", "hashed-pass")).Returns(true);

            var jwtService = new Mock<IJwtTokenService>();
            jwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync("dummy-token");

            var logger = new Mock<ILogger<GetToken>>();

            var handler = new GetToken(context, passwordHasher.Object, jwtService.Object, logger.Object);
            var request = new UserTokenRequest { Email = "test@example.com", Password = "pass" };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task Handle_Returns_Null_For_Invalid_User()
        {
            // Arrange
            var context = CreateInMemoryContext();

            var passwordHasher = new Mock<IPasswordHasher>();
            passwordHasher.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();

            var handler = new GetToken(context, passwordHasher.Object, jwtService.Object, logger.Object);
            var request = new UserTokenRequest { Email = "notfound@example.com", Password = "wrong" };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }
    }
}