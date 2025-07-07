using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class GetTokenHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Token_For_Valid_User()
        {
            // Arrange
            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "pass" };
            var users = new List<User> { user }.AsQueryable();

            var mockSet = new Mock<DbSet<User>>();
            mockSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
            mockSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
            mockSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
            mockSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Users).Returns(mockSet.Object);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["JwtSettings:secretkey"]).Returns("supersecretkeysupersecretkey");
            mockConfig.Setup(c => c["JwtSettings:Issuer"]).Returns("issuer");
            mockConfig.Setup(c => c["JwtSettings:Audience"]).Returns("audience");

            var handler = new GetToken(mockContext.Object, mockConfig.Object);
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
            var users = new List<User>().AsQueryable();

            var mockSet = new Mock<DbSet<User>>();
            mockSet.As<IQueryable<User>>().Setup(m => m.Provider).Returns(users.Provider);
            mockSet.As<IQueryable<User>>().Setup(m => m.Expression).Returns(users.Expression);
            mockSet.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(users.ElementType);
            mockSet.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Users).Returns(mockSet.Object);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["JwtSettings:secretkey"]).Returns("supersecretkeysupersecretkey");
            mockConfig.Setup(c => c["JwtSettings:Issuer"]).Returns("issuer");
            mockConfig.Setup(c => c["JwtSettings:Audience"]).Returns("audience");

            var handler = new GetToken(mockContext.Object, mockConfig.Object);
            var request = new UserTokenRequest { Email = "notfound@example.com", Password = "wrong" };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }
    }
}