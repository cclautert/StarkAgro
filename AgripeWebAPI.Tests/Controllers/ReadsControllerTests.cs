using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Controllers
{
    public class ReadsControllerTests
    {
        private static async IAsyncEnumerable<GetReadResponse> ToAsync(IEnumerable<GetReadResponse> items)
        {
            foreach (var i in items)
            {
                yield return i;
                await Task.Yield();
            }
        }

        [Fact]
        public async Task GetAll_Sets_UserId_And_Returns_Items()
        {
            // Arrange
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new ReadsController(notifier.Object);

            // Set user id claim
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "5") }))
                }
            };

            var command = new GetListReadRequest();

            var responses = new List<GetReadResponse>
            {
                new GetReadResponse { Id = 1 }
            };

            mediator.Setup(m => m.Send(It.Is<GetListReadRequest>(c => c.UserId == 5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ToAsync(responses));

            // Act
            var result = await controller.GetAll(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<GetListReadRequest>(c => c.UserId == 5), It.IsAny<CancellationToken>()), Times.Once);

            var list = new List<GetReadResponse>();
            await foreach (var item in result)
            {
                list.Add(item);
            }

            Assert.Single(list);
            Assert.Equal(1, list[0].Id);
        }

        [Fact]
        public async Task Add_Sets_UserId_And_Invokes_Mediator()
        {
            // Arrange
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new ReadsController(notifier.Object);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "10") }))
                }
            };

            var command = new CreateReadRequest { Code = "SENSOR-1", Value = 12.3m };

            var response = new CreateReadResponse { Id = 42 };

            mediator.Setup(m => m.Send(It.Is<CreateReadRequest>(c => c.UserId == 10 && c.Code == "SENSOR-1"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            // Act
            var result = await controller.Add(mediator.Object, command, CancellationToken.None);

            // Assert
            mediator.Verify(m => m.Send(It.Is<CreateReadRequest>(c => c.UserId == 10 && c.Code == "SENSOR-1"), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
        }
    }
}
