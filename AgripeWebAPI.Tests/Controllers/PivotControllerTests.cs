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
    }
}
