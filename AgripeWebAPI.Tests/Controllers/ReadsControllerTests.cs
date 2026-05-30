using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new ReadsController(notifier.Object);

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

            var result = await controller.GetAll(mediator.Object, command, CancellationToken.None);

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
        public async Task Add_Invokes_Mediator_With_Authenticated_User()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new ReadsController(notifier.Object);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("id", "3") }))
                }
            };

            var command = new CreateReadRequest { Code = "SENSOR-1", Value = 12.3m };
            var response = new CreateReadResponse { Id = 42 };

            mediator.Setup(m => m.Send(It.Is<CreateReadRequest>(c => c.Code == "SENSOR-1"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var result = await controller.Add(mediator.Object, command, CancellationToken.None);

            mediator.Verify(m => m.Send(It.Is<CreateReadRequest>(c => c.Code == "SENSOR-1"), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
        }

        // Regression test: Add must NEVER carry [AllowAnonymous]
        [Fact]
        public void Add_Action_Does_Not_Have_AllowAnonymous_Attribute()
        {
            var method = typeof(ReadsController).GetMethod(nameof(ReadsController.Add));
            Assert.NotNull(method);
            var hasAllowAnonymous = method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0;
            Assert.False(hasAllowAnonymous, "POST /v1/reads/Add must not carry [AllowAnonymous]. See STA-88.");
        }

        [Fact]
        public async Task GetActive_ReturnsOk()
        {
            var notifier = new Mock<INotifier>();
            var controller = new ReadsController(notifier.Object);

            var result = await controller.GetActive(CancellationToken.None);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task GetAllBySensorId_ReturnsItems()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new ReadsController(notifier.Object);

            var responses = new List<GetAllReadBySensorIdResponse>
            {
                new GetAllReadBySensorIdResponse { Id = 1, SensorId = 10, Value = 50m }
            };

            mediator.Setup(m => m.Send(It.IsAny<GetAllListReadBySensorIdRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ToAsyncAll(responses));

            var command = new GetAllListReadBySensorIdRequest { SensorId = 10, Quadrante = 1 };

            var result = await controller.GetAllBySensorId(mediator.Object, command, CancellationToken.None);

            var list = new List<GetAllReadBySensorIdResponse>();
            await foreach (var item in result)
            {
                list.Add(item);
            }
            Assert.Single(list);
            Assert.Equal(1, list[0].Id);
        }

        [Fact]
        public async Task GetByPivotId_ReturnsResponse()
        {
            var notifier = new Mock<INotifier>();
            var mediator = new Mock<IMediator>();
            var controller = new ReadsController(notifier.Object);

            var response = new GetReadByPivotIdResponse { Id = 5, Name = "Pivot1" };

            mediator.Setup(m => m.Send(It.IsAny<GetListReadByPivotIdRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var command = new GetListReadByPivotIdRequest { PivotId = 5 };

            var result = await controller.GetByPivotId(mediator.Object, command, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(5, result.Id);
            Assert.Equal("Pivot1", result.Name);
        }

        private static async IAsyncEnumerable<GetAllReadBySensorIdResponse> ToAsyncAll(IEnumerable<GetAllReadBySensorIdResponse> items)
        {
            foreach (var i in items)
            {
                yield return i;
                await Task.Yield();
            }
        }
    }
}
