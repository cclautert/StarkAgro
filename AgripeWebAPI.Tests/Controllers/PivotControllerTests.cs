using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgripeWebAPI.Tests.Mocks;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Controllers
{
    public class PivotControllerTests
    {
        [Fact]
        public async Task GetAll_Sets_UserId_And_Returns_CustomResponse()
        {
            // Arrange
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new PivotController(notifier.Object);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "7") }))
                }
            };

            var command = new GetListPivotByUserIdRequest();

            var responses = new List<GetPivotResponse>
            {
                new GetPivotResponse { Id = 3 }
            };

            mediator.Setup(m => m.Send(It.Is<GetListPivotByUserIdRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()))
                .ReturnsAsync(responses);

            // Act
            var actionResult = await controller.GetAll(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<GetListPivotByUserIdRequest>(c => c.UserId == 7), It.IsAny<CancellationToken>()), Times.Once);

            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(200, objectResult.StatusCode);

            var returned = objectResult.Value as List<GetPivotResponse>;
            Assert.NotNull(returned);

            Assert.Single(returned);
            Assert.Equal(3, returned[0].Id);
        }

        [Fact]
        public async Task Add_Sets_UserId_And_Returns_CreateResponse()
        {
            // Arrange
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new PivotController(notifier.Object);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "2") }))
                }
            };

            var command = new CreatePivotRequest { Name = "Pivot A" };
            var expected = new CreatePivotResponse { Id = 99 };

            mediator.Setup(m => m.Send(It.Is<CreatePivotRequest>(c => c.UserId == 2 && c.Name == "Pivot A"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var actionResult = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<CreatePivotRequest>(c => c.UserId == 2 && c.Name == "Pivot A"), It.IsAny<CancellationToken>()), Times.Once);
            var result = Assert.IsType<CreatePivotResponse>(actionResult.Value);
            Assert.Equal(99, result.Id);
        }
        private PivotController CreateControllerWithClaim(INotifier notifier, string userId = "7")
        {
            var controller = new PivotController(notifier);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", userId) }))
                }
            };
            return controller;
        }

        [Fact]
        public async Task GetById_Valid_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            var expected = new GetPivotResponse { Id = 3, Name = "Pivot 3" };
            mediator.Setup(m => m.Send(It.IsAny<GetPivotRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.GetById(mediator.Object, new GetPivotRequest(), CancellationToken.None);

            var response = Assert.IsType<GetPivotResponse>(result.Value);
            Assert.Equal(3, response.Id);
        }

        [Fact]
        public async Task GetById_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("Id", "Required");

            var result = await controller.GetById(mediator.Object, new GetPivotRequest(), CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_Valid_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            var expected = new EditPivotResponse { Id = 3 };
            mediator.Setup(m => m.Send(It.IsAny<EditPivotRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.Update(mediator.Object, new EditPivotRequest { Name = "Updated" }, CancellationToken.None);

            var response = Assert.IsType<EditPivotResponse>(result.Value);
            Assert.Equal(3, response.Id);
        }

        [Fact]
        public async Task Update_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("Name", "Required");

            var result = await controller.Update(mediator.Object, new EditPivotRequest(), CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Delete_Valid_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier.Object);

            var expected = new DeletePivotResponse();
            mediator.Setup(m => m.Send(It.IsAny<DeletePivotRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await controller.Delete(mediator.Object, new DeletePivotRequest { Id = 3 }, CancellationToken.None);

            Assert.IsType<DeletePivotResponse>(result.Value);
        }

        [Fact]
        public async Task Delete_InvalidModelState_ReturnsBadRequest()
        {
            var notifier = new MockNotifier();
            var mediator = new Mock<IMediator>();
            var controller = CreateControllerWithClaim(notifier);
            controller.ModelState.AddModelError("Id", "Required");

            var result = await controller.Delete(mediator.Object, new DeletePivotRequest(), CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
