using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Controllers
{
    public class AuthControllerTests
    {
        private static AuthController CreateController(MockNotifier notifier)
        {
            var controller = new AuthController(notifier);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "7") }))
                }
            };
            return controller;
        }

        [Fact]
        public async Task LogIn_Valid_ReturnsOkWithToken()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new UserTokenRequest { Email = "test@example.com", Password = "pass123" };
            var expected = new UserTokenResponse { Token = "jwt-token-abc" };

            mediator.Setup(m => m.Send(It.IsAny<UserTokenRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.LogIn(mediator.Object, command, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<UserTokenResponse>(okResult.Value);
            Assert.Equal("jwt-token-abc", response.Token);
        }

        [Fact]
        public async Task LogIn_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new UserTokenRequest { Email = "test@example.com", Password = "wrong" };

            mediator.Setup(m => m.Send(It.IsAny<UserTokenRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserTokenResponse { ErrorCode = LoginErrorCode.InvalidCredentials });

            // Act
            var result = await controller.LogIn(mediator.Object, command, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(401, statusResult.StatusCode);
        }

        [Fact]
        public async Task LogIn_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Email", "Email is required");

            var command = new UserTokenRequest();

            // Act
            var result = await controller.LogIn(mediator.Object, command, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task LogIn_AccountInactive_ReturnsForbidden()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new UserTokenRequest { Email = "inactive@example.com", Password = "pass" };

            mediator.Setup(m => m.Send(It.IsAny<UserTokenRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserTokenResponse { ErrorCode = LoginErrorCode.AccountInactive });

            // Act
            var result = await controller.LogIn(mediator.Object, command, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, statusResult.StatusCode);
        }

        [Fact]
        public async Task LogIn_TooManyAttempts_ReturnsTooManyRequests()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new UserTokenRequest { Email = "locked@example.com", Password = "pass" };

            mediator.Setup(m => m.Send(It.IsAny<UserTokenRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserTokenResponse { ErrorCode = LoginErrorCode.TooManyAttempts });

            // Act
            var result = await controller.LogIn(mediator.Object, command, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(429, statusResult.StatusCode);
        }

        [Fact]
        public async Task ExternalLogin_ValidCode_ReturnsOkWithToken()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new ExternalLoginRequest
            {
                Provider = "Google",
                Code = "auth-code-123",
                RedirectUri = "https://localhost:4200/login/callback"
            };
            var expected = new UserTokenResponse { Token = "jwt-external-token" };

            mediator.Setup(m => m.Send(It.IsAny<ExternalLoginRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.ExternalLogin(mediator.Object, command, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<UserTokenResponse>(okResult.Value);
            Assert.Equal("jwt-external-token", response.Token);
        }

        [Fact]
        public async Task ExternalLogin_NullCode_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new ExternalLoginRequest { Provider = "Google", Code = "", RedirectUri = "" };

            // Act
            var result = await controller.ExternalLogin(mediator.Object, command, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact]
        public async Task ExternalLogin_MediatorReturnsNull_ReturnsUnauthorized()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new ExternalLoginRequest
            {
                Provider = "Google",
                Code = "auth-code-123",
                RedirectUri = "https://localhost:4200/login/callback"
            };

            mediator.Setup(m => m.Send(It.IsAny<ExternalLoginRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserTokenResponse)null);

            // Act
            var result = await controller.ExternalLogin(mediator.Object, command, CancellationToken.None);

            // Assert — handler failure returns 401, not 400
            var unauthorized = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(401, unauthorized.StatusCode);
        }

        [Fact]
        public async Task ExternalLogin_AccountInactive_ReturnsForbidden()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new ExternalLoginRequest
            {
                Provider = "Google",
                Code = "auth-code-123",
                RedirectUri = "https://localhost:4200/login/callback"
            };

            mediator.Setup(m => m.Send(It.IsAny<ExternalLoginRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserTokenResponse { ErrorCode = LoginErrorCode.AccountInactive });

            // Act
            var result = await controller.ExternalLogin(mediator.Object, command, CancellationToken.None);

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, statusResult.StatusCode);
        }

        [Fact]
        public async Task AddUser_Valid_ReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new CreateUserRequest { Name = "New User", Email = "new@example.com", Password = "Pass123!" };
            var expected = new CreateUserResponse { Id = 1, Name = "New User", Email = "new@example.com" };

            mediator.Setup(m => m.Send(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.AddUser(mediator.Object, command, CancellationToken.None);

            // Assert
            var response = Assert.IsType<CreateUserResponse>(result.Value);
            Assert.Equal(1, response.Id);
            Assert.Equal("New User", response.Name);
        }

        [Fact]
        public async Task AddUser_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Name", "Name is required");

            var command = new CreateUserRequest();

            // Act
            var result = await controller.AddUser(mediator.Object, command, CancellationToken.None);

            // Assert
            var badRequest = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, badRequest.StatusCode);
        }
    }
}
