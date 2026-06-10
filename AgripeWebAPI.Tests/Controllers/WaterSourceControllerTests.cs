using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.WaterSources;
using AgripeWebAPI.Domain.Commands.Responses.WaterSources;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AgripeWebAPI.Tests.Controllers
{
    public class WaterSourceControllerTests
    {
        private static WaterSourceController CreateController(INotifier? notifier = null)
        {
            var controller = new WaterSourceController(notifier ?? new Mock<INotifier>().Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };
            return controller;
        }

        // ─── GetAll ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_ReturnsMediatorResult()
        {
            var mediator = new Mock<IMediator>();
            var expected = new List<WaterSourceResponse> { new() { Id = 1, Name = "W1" } };
            mediator.Setup(m => m.Send(It.IsAny<GetListWaterSourceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = CreateController();
            var result = await controller.GetAll(mediator.Object, CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Same(expected, obj.Value);
        }

        // ─── GetById ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GetById_Found_ReturnsResponse()
        {
            var mediator = new Mock<IMediator>();
            var expected = new WaterSourceResponse { Id = 5, Name = "W5" };
            mediator.Setup(m => m.Send(It.Is<GetWaterSourceRequest>(r => r.Id == 5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = CreateController();
            var result = await controller.GetById(mediator.Object, 5, CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Same(expected, obj.Value);
        }

        // ─── Create ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Create_Valid_ReturnsMediatorResult()
        {
            var mediator = new Mock<IMediator>();
            var expected = new WaterSourceResponse { Id = 10, Name = "New" };
            mediator.Setup(m => m.Send(It.IsAny<CreateWaterSourceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = CreateController();
            var result = await controller.Create(mediator.Object, new CreateWaterSourceRequest { Name = "New", MaxFlowLitersPerHour = 500 }, CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Same(expected, obj.Value);
        }

        [Fact]
        public async Task Create_InvalidModelState_ReturnsBadRequest()
        {
            var mediator = new Mock<IMediator>();
            var controller = CreateController(new MockNotifier());
            controller.ModelState.AddModelError("Name", "Required");

            var result = await controller.Create(mediator.Object, new CreateWaterSourceRequest(), CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, obj.StatusCode);
            mediator.Verify(m => m.Send(It.IsAny<CreateWaterSourceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ─── Update ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Update_Valid_SetsIdAndReturnsMediatorResult()
        {
            var mediator = new Mock<IMediator>();
            var expected = new WaterSourceResponse { Id = 3, Name = "Updated" };
            mediator.Setup(m => m.Send(It.Is<EditWaterSourceRequest>(r => r.Id == 3), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = CreateController();
            var result = await controller.Update(mediator.Object, 3, new EditWaterSourceRequest { Name = "Updated", MaxFlowLitersPerHour = 200 }, CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Same(expected, obj.Value);
        }

        [Fact]
        public async Task Update_InvalidModelState_ReturnsBadRequest()
        {
            var mediator = new Mock<IMediator>();
            var controller = CreateController(new MockNotifier());
            controller.ModelState.AddModelError("Name", "Required");

            var result = await controller.Update(mediator.Object, 1, new EditWaterSourceRequest(), CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, obj.StatusCode);
        }

        // ─── Delete ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_Existing_ReturnsTrue()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.Is<DeleteWaterSourceRequest>(r => r.Id == 7), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var controller = CreateController();
            var result = await controller.Delete(mediator.Object, 7, CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Equal(true, obj.Value);
        }

        [Fact]
        public async Task Delete_NotFound_ReturnsFalse()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<DeleteWaterSourceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var controller = CreateController();
            var result = await controller.Delete(mediator.Object, 99, CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Equal(false, obj.Value);
        }
    }
}
