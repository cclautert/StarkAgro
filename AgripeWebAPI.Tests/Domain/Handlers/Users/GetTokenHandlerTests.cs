using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class GetTokenHandlerTests
    {
        private static GetToken CreateHandler(
            Mock<agpDBContext> mockDbContext,
            Mock<IPasswordHasher> passwordHasher,
            Mock<IJwtTokenService> jwtService,
            Mock<ILogger<GetToken>> logger,
            Mock<ILoginAttemptService> loginAttemptService)
        {
            return new GetToken(
                mockDbContext.Object,
                passwordHasher.Object,
                jwtService.Object,
                logger.Object,
                loginAttemptService.Object);
        }

        [Fact]
        public async Task Handle_Returns_Token_For_Valid_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();
            var loginAttemptService = new Mock<ILoginAttemptService>();

            var existingHash = "$2b$10$ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmno";
            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = existingHash, Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("pass", existingHash)).Returns(true);
            jwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("dummy-token");
            loginAttemptService.Setup(l => l.IsLockedOut(It.IsAny<string>())).Returns(false);

            var handler = CreateHandler(mockDbContext, passwordHasher, jwtService, logger, loginAttemptService);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "pass" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Token));
            Assert.Equal(LoginErrorCode.None, result.ErrorCode);
        }

        [Fact]
        public async Task Handle_Returns_InvalidCredentials_For_Unknown_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();
            var loginAttemptService = new Mock<ILoginAttemptService>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            loginAttemptService.Setup(l => l.IsLockedOut(It.IsAny<string>())).Returns(false);

            var handler = CreateHandler(mockDbContext, passwordHasher, jwtService, logger, loginAttemptService);
            var result = await handler.Handle(new UserTokenRequest { Email = "notfound@example.com", Password = "wrong" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(LoginErrorCode.InvalidCredentials, result.ErrorCode);
        }

        [Fact]
        public async Task Handle_InactiveUser_ReturnsAccountInactive()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();
            var loginAttemptService = new Mock<ILoginAttemptService>();

            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "pass", Active = false };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            loginAttemptService.Setup(l => l.IsLockedOut(It.IsAny<string>())).Returns(false);

            var handler = CreateHandler(mockDbContext, passwordHasher, jwtService, logger, loginAttemptService);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "pass" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(LoginErrorCode.AccountInactive, result.ErrorCode);
        }

        [Fact]
        public async Task Handle_WrongPassword_ReturnsInvalidCredentials()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();
            var loginAttemptService = new Mock<ILoginAttemptService>();

            var existingHash = "$2b$10$ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmno";
            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = existingHash, Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("wrongpass", existingHash)).Returns(false);
            loginAttemptService.Setup(l => l.IsLockedOut(It.IsAny<string>())).Returns(false);

            var handler = CreateHandler(mockDbContext, passwordHasher, jwtService, logger, loginAttemptService);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "wrongpass" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(LoginErrorCode.InvalidCredentials, result.ErrorCode);
        }

        [Fact]
        public async Task Handle_PlainTextPassword_MigratesToBcrypt()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();
            var loginAttemptService = new Mock<ILoginAttemptService>();

            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "plaintext", Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("plaintext", "plaintext")).Returns(true);
            passwordHasher.Setup(p => p.HashPassword("plaintext")).Returns("$2b$10$hashedvalue");
            jwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("dummy-token");
            loginAttemptService.Setup(l => l.IsLockedOut(It.IsAny<string>())).Returns(false);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            var handler = CreateHandler(mockDbContext, passwordHasher, jwtService, logger, loginAttemptService);
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
            var loginAttemptService = new Mock<ILoginAttemptService>();

            var user = new User { Id = 1, Name = "Test", Email = "test@example.com", Password = "", Active = true };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);
            passwordHasher.Setup(p => p.VerifyPassword("pass", "")).Returns(true);
            passwordHasher.Setup(p => p.HashPassword("pass")).Returns("$2b$10$newhash");
            jwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("token");
            loginAttemptService.Setup(l => l.IsLockedOut(It.IsAny<string>())).Returns(false);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            var handler = CreateHandler(mockDbContext, passwordHasher, jwtService, logger, loginAttemptService);
            var result = await handler.Handle(new UserTokenRequest { Email = "test@example.com", Password = "pass" }, CancellationToken.None);

            Assert.NotNull(result);
            mockUsers.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_LockedOut_ReturnsTooManyAttempts()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var passwordHasher = new Mock<IPasswordHasher>();
            var jwtService = new Mock<IJwtTokenService>();
            var logger = new Mock<ILogger<GetToken>>();
            var loginAttemptService = new Mock<ILoginAttemptService>();

            loginAttemptService.Setup(l => l.IsLockedOut("locked@example.com")).Returns(true);

            var handler = CreateHandler(mockDbContext, passwordHasher, jwtService, logger, loginAttemptService);
            var result = await handler.Handle(new UserTokenRequest { Email = "locked@example.com", Password = "any" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(LoginErrorCode.TooManyAttempts, result.ErrorCode);
        }
    }
}
