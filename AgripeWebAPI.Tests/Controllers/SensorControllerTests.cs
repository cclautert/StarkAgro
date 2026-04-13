using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Controllers
{
    public class SensorControllerTests
    {
        private static SensorController CreateController(MockNotifier notifier)
        {
            var controller = new SensorController(notifier);
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

            var command = new GetSensorRequest { Id = 1 };
            var expected = new GetSensorResponse { Id = 1, Name = "Sensor A" };

            mediator.Setup(m => m.Send(It.IsAny<GetSensorRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.GetById(mediator.Object, command, CancellationToken.None);

            // Assert
            var response = Assert.IsType<GetSensorResponse>(result.Value);
            Assert.Equal(1, response.Id);
            Assert.Equal("Sensor A", response.Name);
        }

        [Fact]
        public async Task GetById_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Id", "Id is required");

            var command = new GetSensorRequest();

            // Act
            var result = await controller.GetById(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetAll_SetsUserId_ReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new GetListSensorByUserIdRequest();
            var responses = new List<GetSensorResponse>
            {
                new GetSensorResponse { Id = 1, Name = "Sensor A" }
            };

            mediator.Setup(m => m.Send(It.Is<GetListSensorByUserIdRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()))
                .ReturnsAsync(responses);

            // Act
            var result = await controller.getAll(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<GetListSensorByUserIdRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task GetAllByPivotId_ReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new GetListSensorRequest { PivotId = 5, Quadrante = 1 };
            var responses = new List<GetSensorResponse>
            {
                new GetSensorResponse { Id = 2, Name = "Sensor B" }
            };

            mediator.Setup(m => m.Send(It.IsAny<GetListSensorRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(responses);

            // Act
            var result = await controller.getAllByPivotId(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.Equal(2, result[0].Id);
        }

        [Fact]
        public async Task Add_SetsUserId_ReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new CreateSensorRequest { Name = "New Sensor", Code = "5C:CF:7F:3A:54:29" };
            var expected = new CreateSensorResponse { Id = 10 };

            mediator.Setup(m => m.Send(It.Is<CreateSensorRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<CreateSensorRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()), Times.Once);
            var response = Assert.IsType<CreateSensorResponse>(result.Value);
            Assert.Equal(10, response.Id);
        }

        [Fact]
        public async Task Add_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Name", "Name is required");

            var command = new CreateSensorRequest();

            // Act
            var result = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Add_WithValidMacCode_ReturnsSuccess()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new CreateSensorRequest { Name = "Sensor Norte", Code = "5C:CF:7F:3A:54:29" };
            var expected = new CreateSensorResponse { Id = 20 };

            mediator.Setup(m => m.Send(It.IsAny<CreateSensorRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            var response = Assert.IsType<CreateSensorResponse>(result.Value);
            Assert.Equal(20, response.Id);
        }

        [Fact]
        public async Task Update_WithInvalidMacCode_ModelStateError_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Code",
                "Code must be a valid MAC address in the format XX:XX:XX:XX:XX:XX.");

            var command = new EditSensorRequest { Id = 1, Code = "NOT-A-MAC" };

            // Act
            var result = await controller.Update(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_SetsUserId_ReturnsResponse()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);

            var command = new EditSensorRequest { Id = 3, Name = "Updated Sensor" };
            var expected = new EditSensorResponse { Id = 3 };

            mediator.Setup(m => m.Send(It.Is<EditSensorRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await controller.Update(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<EditSensorRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()), Times.Once);
            var response = Assert.IsType<EditSensorResponse>(result.Value);
            Assert.Equal(3, response.Id);
        }

        [Fact]
        public async Task Update_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateController(notifier);
            controller.ModelState.AddModelError("Name", "Name is required");

            var command = new EditSensorRequest();

            // Act
            var result = await controller.Update(mediator.Object, command, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
