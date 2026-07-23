using StarkAgroAPI.Controllers;
using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace StarkAgroAPI.Tests.Controllers
{
    public class NdviControllerTests
    {
        private static NdviController CreateController() => new(new MockNotifier());

        [Fact]
        public async Task List_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<ListMonitoredAreasRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MonitoredAreaResponse> { new MonitoredAreaResponse { Id = 1 } });

            var result = await CreateController().List(mediator.Object, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }

        [Fact]
        public async Task Trend_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetNdviTrendRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NdviTrendResponse { AreaId = 5 });

            var result = await CreateController().Trend(mediator.Object, 5, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }

        [Fact]
        public async Task History_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<FetchNdviHistoryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FetchNdviHistoryResponse { FetchedFromCdse = true, NearestDate = "2026-06-06" });

            var result = await CreateController().History(mediator.Object, 5, new DateTime(2026, 6, 8), default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }

        [Fact]
        public async Task Overlay_Found_ReturnsFile()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetNdviOverlayImageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NdviOverlayImageResponse { Content = [1, 2, 3], ContentType = "image/png" });

            var controller = CreateController();
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };

            var result = await controller.Overlay(mediator.Object, 5, 3, default);

            var file = Assert.IsType<Microsoft.AspNetCore.Mvc.FileContentResult>(result);
            Assert.Equal("image/png", file.ContentType);
        }

        [Fact]
        public async Task Overlay_NotFound_Returns404()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetNdviOverlayImageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NdviOverlayImageResponse?)null);

            var result = await CreateController().Overlay(mediator.Object, 5, 3, default);

            Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
        }

        [Fact]
        public async Task Get_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetMonitoredAreaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MonitoredAreaResponse { Id = 5 });

            var result = await CreateController().Get(mediator.Object, 5, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }

        [Fact]
        public async Task Create_ReturnsCreated()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<CreateMonitoredAreaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MonitoredAreaResponse { Id = 9 });

            var result = await CreateController().Create(mediator.Object,
                new CreateMonitoredAreaRequest { Name = "X", AreaKind = "Polygon" }, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(201, obj.StatusCode);
        }

        [Fact]
        public async Task Create_InvalidModel_ReturnsBadRequest()
        {
            var controller = CreateController();
            controller.ModelState.AddModelError("Name", "required");

            var result = await controller.Create(new Mock<IMediator>().Object, new CreateMonitoredAreaRequest(), default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, obj.StatusCode);
        }

        [Fact]
        public async Task Edit_SetsIdAndReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<EditMonitoredAreaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MonitoredAreaResponse { Id = 7 });

            var command = new EditMonitoredAreaRequest { Name = "X", AreaKind = "Polygon" };
            var result = await CreateController().Edit(mediator.Object, 7, command, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Equal(7, command.Id);
        }

        [Fact]
        public async Task Delete_Ok_Returns204()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<DeleteMonitoredAreaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await CreateController().Delete(mediator.Object, 3, default);

            var sc = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(204, sc.StatusCode);
        }

        [Fact]
        public async Task Delete_False_Returns400()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<DeleteMonitoredAreaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await CreateController().Delete(mediator.Object, 3, default);

            var sc = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(400, sc.StatusCode);
        }
    }
}
