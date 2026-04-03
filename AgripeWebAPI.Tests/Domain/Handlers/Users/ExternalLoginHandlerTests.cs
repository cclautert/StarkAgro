using AgripeWebAPI.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using System.Net;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class ExternalLoginHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<User>> _mockUsers;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IJwtTokenService> _mockJwtTokenService;
        private readonly Mock<IPasswordHasher> _mockPasswordHasher;
        private readonly Mock<ILogger<ExternalLoginHandler>> _mockLogger;
        private readonly IOptions<OAuthSettings> _defaultOAuthSettings;

        public ExternalLoginHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockUsers = new Mock<IMongoCollection<User>>();
            _mockDbContext.Setup(c => c.Users).Returns(_mockUsers.Object);

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockJwtTokenService = new Mock<IJwtTokenService>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();
            _mockLogger = new Mock<ILogger<ExternalLoginHandler>>();

            _defaultOAuthSettings = Options.Create(new OAuthSettings
            {
                Google = new GoogleOAuthSettings
                {
                    ClientId = "client-id",
                    ClientSecret = "client-secret",
                    AllowedRedirectUris = "http://localhost:4200/login/callback"
                }
            });
        }

        private ExternalLoginHandler CreateHandler(IOptions<OAuthSettings>? oauthSettings = null, IHttpClientFactory? httpClientFactory = null)
        {
            return new ExternalLoginHandler(
                _mockDbContext.Object,
                httpClientFactory ?? _mockHttpClientFactory.Object,
                _mockJwtTokenService.Object,
                _mockPasswordHasher.Object,
                oauthSettings ?? _defaultOAuthSettings,
                _mockLogger.Object);
        }

        private void SetupHttpClient(MockHttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        }

        private MockHttpMessageHandler CreateDefaultHappyPathHandler()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, "{\"access_token\":\"test-access-token\"}");
            handler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"123\",\"email\":\"test@gmail.com\",\"name\":\"Test User\"}");
            return handler;
        }

        private ExternalLoginRequest CreateDefaultRequest()
        {
            return new ExternalLoginRequest
            {
                Provider = "Google",
                Code = "auth-code-123",
                RedirectUri = "http://localhost:4200/login/callback"
            };
        }

        [Fact]
        public async Task Handle_MissingProvider_ReturnsNull()
        {
            // Arrange
            var handler = CreateHandler();
            var request = new ExternalLoginRequest { Provider = "", Code = "some-code", RedirectUri = "http://localhost" };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_MissingCode_ReturnsNull()
        {
            // Arrange
            var handler = CreateHandler();
            var request = new ExternalLoginRequest { Provider = "Google", Code = "", RedirectUri = "http://localhost" };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_UnsupportedProvider_ReturnsNull()
        {
            // Arrange
            var handler = CreateHandler();
            var request = new ExternalLoginRequest { Provider = "Facebook", Code = "some-code", RedirectUri = "http://localhost" };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_MissingGoogleConfig_ReturnsNull()
        {
            // Arrange
            var settings = Options.Create(new OAuthSettings
            {
                Google = new GoogleOAuthSettings
                {
                    ClientId = null,
                    ClientSecret = null,
                    AllowedRedirectUris = null
                }
            });
            var handler = CreateHandler(oauthSettings: settings);
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_DisallowedRedirectUri_ReturnsNull()
        {
            // Arrange
            var settings = Options.Create(new OAuthSettings
            {
                Google = new GoogleOAuthSettings
                {
                    ClientId = "client-id",
                    ClientSecret = "client-secret",
                    AllowedRedirectUris = "https://allowed.example.com/callback"
                }
            });
            var handler = CreateHandler(oauthSettings: settings);
            var request = new ExternalLoginRequest
            {
                Provider = "Google",
                Code = "auth-code-123",
                RedirectUri = "http://evil.example.com/callback"
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_GoogleTokenExchangeFails_ReturnsNull()
        {
            // Arrange
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler);
            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var handler = CreateHandler(httpClientFactory: httpClientFactory.Object);
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_GoogleTokenResponseNon200_ReturnsNull()
        {
            // Arrange
            var msgHandler = new MockHttpMessageHandler();
            msgHandler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}");
            SetupHttpClient(msgHandler);

            var handler = CreateHandler();
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_GoogleTokenMissingAccessToken_ReturnsNull()
        {
            // Arrange
            var msgHandler = new MockHttpMessageHandler();
            msgHandler.EnqueueResponse(HttpStatusCode.OK, "{}");
            SetupHttpClient(msgHandler);

            var handler = CreateHandler();
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_GoogleUserInfoFails_ReturnsNull()
        {
            // Arrange
            var msgHandler = new MockHttpMessageHandler();
            msgHandler.EnqueueResponse(HttpStatusCode.OK, "{\"access_token\":\"test\"}");
            msgHandler.EnqueueResponse(HttpStatusCode.InternalServerError, "error");
            SetupHttpClient(msgHandler);

            var handler = CreateHandler();
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_GoogleUserInfoMissingEmail_ReturnsNull()
        {
            // Arrange
            var msgHandler = new MockHttpMessageHandler();
            msgHandler.EnqueueResponse(HttpStatusCode.OK, "{\"access_token\":\"test-access-token\"}");
            msgHandler.EnqueueResponse(HttpStatusCode.OK, "{\"id\":\"123\"}");
            SetupHttpClient(msgHandler);

            var handler = CreateHandler();
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NewUser_CreatesAndReturnsToken()
        {
            // Arrange
            var msgHandler = CreateDefaultHappyPathHandler();
            SetupHttpClient(msgHandler);

            MongoMockHelper.SetupFind<User>(_mockUsers, null); // No existing user
            _mockDbContext.Setup(c => c.GetNextIdAsync("User", It.IsAny<CancellationToken>())).ReturnsAsync(42);
            _mockUsers.Setup(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockPasswordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed-random");
            _mockJwtTokenService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("jwt-token-123");

            var handler = CreateHandler();
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("jwt-token-123", result.Token);
            _mockUsers.Verify(c => c.InsertOneAsync(
                It.Is<User>(u => u.Email == "test@gmail.com" && u.Name == "Test User" && u.Active),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ExistingActiveUser_ReturnsToken()
        {
            // Arrange
            var msgHandler = CreateDefaultHappyPathHandler();
            SetupHttpClient(msgHandler);

            var existingUser = new User { Id = 10, Name = "Existing", Email = "test@gmail.com", Password = "hashed", Active = true };
            MongoMockHelper.SetupFind(_mockUsers, existingUser);
            _mockJwtTokenService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("jwt-token-existing");

            var handler = CreateHandler();
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("jwt-token-existing", result.Token);
            _mockUsers.Verify(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_InactiveUser_ReturnsNull()
        {
            // Arrange
            var msgHandler = CreateDefaultHappyPathHandler();
            SetupHttpClient(msgHandler);

            var inactiveUser = new User { Id = 10, Name = "Inactive", Email = "test@gmail.com", Password = "hashed", Active = false };
            MongoMockHelper.SetupFind(_mockUsers, inactiveUser);

            var handler = CreateHandler();
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LoginErrorCode.AccountInactive, result.ErrorCode);
        }

        [Fact]
        public async Task Handle_GoogleUserInfoThrowsException_ReturnsNull()
        {
            // Arrange - first call succeeds (token exchange), second throws (userinfo)
            var msgHandler = new MockHttpMessageHandler();
            msgHandler.EnqueueResponse(HttpStatusCode.OK, "{\"access_token\":\"test\"}");
            // The second call should throw. We use a custom handler for this.
            var throwOnSecondHandler = new ThrowOnSecondCallHandler();
            var httpClient = new HttpClient(throwOnSecondHandler);
            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var handler = CreateHandler(httpClientFactory: httpClientFactory.Object);
            var request = CreateDefaultRequest();

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NoAllowedRedirectUris_AcceptsAnyUri()
        {
            // Arrange - AllowedRedirectUris is null (no restriction)
            var settings = Options.Create(new OAuthSettings
            {
                Google = new GoogleOAuthSettings
                {
                    ClientId = "client-id",
                    ClientSecret = "client-secret",
                    AllowedRedirectUris = null
                }
            });

            var msgHandler = CreateDefaultHappyPathHandler();
            SetupHttpClient(msgHandler);

            MongoMockHelper.SetupFind<User>(_mockUsers, null);
            _mockDbContext.Setup(c => c.GetNextIdAsync("User", It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockUsers.Setup(c => c.InsertOneAsync(It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockPasswordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");
            _mockJwtTokenService.Setup(j => j.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("jwt-token");

            var handler = CreateHandler(oauthSettings: settings);
            var request = new ExternalLoginRequest
            {
                Provider = "Google",
                Code = "auth-code",
                RedirectUri = "http://any-uri.example.com/callback"
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("jwt-token", result.Token);
        }

        /// <summary>
        /// Helper that throws HttpRequestException on any request.
        /// </summary>
        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new HttpRequestException("Simulated network failure");
            }
        }

        /// <summary>
        /// Returns a successful token response on first call, throws on second call (userinfo).
        /// </summary>
        private sealed class ThrowOnSecondCallHandler : HttpMessageHandler
        {
            private int _callCount;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _callCount++;
                if (_callCount == 1)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"access_token\":\"test\"}", System.Text.Encoding.UTF8, "application/json")
                    });
                }
                throw new HttpRequestException("Simulated userinfo failure");
            }
        }
    }
}
