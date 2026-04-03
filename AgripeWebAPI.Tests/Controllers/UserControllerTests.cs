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
    public class UserControllerTests
    {
        private static UserController CreateController(MockNotifier notifier)
        {
            var controller = new UserController(notifier);
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
        public async Task GetById_Valid_ReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new GetUserRequest { Id = 7 };
            var expected = new GetUserResponse { Id = 7, Name = "Test User", Email = "test@example.com" };

            mediator.Setup(m => m.Send(It.IsAny<GetUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.GetById(mediator.Object, command, CancellationToken.None);

            // Assert
            var response = Assert.IsType<GetUserResponse>(result.Value);
            Assert.Equal(7, response.Id);
            Assert.Equal("Test User", response.Name);
        }

        [Fact]
        public async Task GetById_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Id", "Id is required");

            var command = new GetUserRequest();

            // Act
            var result = await controller.GetById(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Add_Valid_ReturnsResponse()
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
            var result = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            var response = Assert.IsType<CreateUserResponse>(result.Value);
            Assert.Equal(1, response.Id);
            Assert.Equal("New User", response.Name);
        }

        [Fact]
        public async Task Add_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Name", "Name is required");

            var command = new CreateUserRequest();

            // Act
            var result = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_Valid_ReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new EditUserRequest { Name = "Updated", Email = "updated@example.com", Password = "Pass123!" };
            var expected = new EditUserResponse { Id = 7, Name = "Updated", Email = "updated@example.com" };

            mediator.Setup(m => m.Send(It.IsAny<EditUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.Update(mediator.Object, command, CancellationToken.None);

            // Assert
            var response = Assert.IsType<EditUserResponse>(result.Value);
            Assert.Equal(7, response.Id);
            Assert.Equal("Updated", response.Name);
        }

        [Fact]
        public async Task Update_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Name", "Name is required");

            var command = new EditUserRequest();

            // Act
            var result = await controller.Update(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateLimits_Valid_SetsUserIdAndReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new EditUserLimitsRequest { LimiteInferior = 10m, LimiteSuperior = 90m };
            var expected = new EditUserResponse { Id = 7, Name = "Test", Email = "t@t.com" };

            mediator.Setup(m => m.Send(It.Is<EditUserLimitsRequest>(r => r.Id == 7), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.UpdateLimits(mediator.Object, command, CancellationToken.None);

            // Assert
            var response = Assert.IsType<EditUserResponse>(result.Value);
            Assert.Equal(7, response.Id);
            mediator.Verify(m => m.Send(It.Is<EditUserLimitsRequest>(r => r.Id == 7), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateLimits_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("LimiteInferior", "Required");

            // Act
            var result = await controller.UpdateLimits(mediator.Object, new EditUserLimitsRequest(), CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
