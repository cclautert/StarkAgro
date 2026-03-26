using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class GetTokenHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Token_For_Valid_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();

            var existingHash = "$2b$10$ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmno";
            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = existingHash, Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("pass", existingHash)).Returns(true);
            jwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("dummy-token");

            var handler = new GetToken(mockDbContext.Object, passwordHasher.Object, jwtService.Object, logger.Object);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "pass" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task Handle_Returns_Null_For_Invalid_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new GetToken(mockDbContext.Object, passwordHasher.Object, jwtService.Object, logger.Object);
            var result = await handler.Handle(new UserTokenRequest { Email = "notfound@example.com", Password = "wrong" }, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_InactiveUser_ReturnsNull()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();

            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "pass", Active = false };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new GetToken(mockDbContext.Object, passwordHasher.Object, jwtService.Object, logger.Object);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "pass" }, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_WrongPassword_ReturnsNull()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();

            var existingHash = "$2b$10$ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmno";
            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = existingHash, Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("wrongpass", existingHash)).Returns(false);

            var handler = new GetToken(mockDbContext.Object, passwordHasher.Object, jwtService.Object, logger.Object);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "wrongpass" }, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_PlainTextPassword_MigratesToBcrypt()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();

            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "plaintext", Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("plaintext", "plaintext")).Returns(true);
            passwordHasher.Setup(p => p.HashPassword("plaintext")).Returns("$2b$10$hashedvalue");
            jwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("dummy-token");
            mockUsers.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            var handler = new GetToken(mockDbContext.Object, passwordHasher.Object, jwtService.Object, logger.Object);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "plaintext" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("dummy-token", result.Token);
            mockUsers.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_EmptyPasswordHash_MigratesToBcrypt()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();

            // User with empty password hash (IsBcryptHash returns false via null/whitespace path)
            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "", Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("pass", "")).Returns(true);
            passwordHasher.Setup(p => p.HashPassword("pass")).Returns("$2b$10$newhash");
            jwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("token");
            mockUsers.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            var handler = new GetToken(mockDbContext.Object, passwordHasher.Object, jwtService.Object, logger.Object);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "pass" }, CancellationToken.None);

            Assert.NotNull(result);
            mockUsers.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
