using AgripeWebAPI.Controllers;
using AgripeWebAPI.Domain.Commands.Requests.Irrigation;
using AgripeWebAPI.Domain.Commands.Responses.Irrigation;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AgripeWebAPI.Tests.Controllers
{
    public class IrrigationControllerTests
    {
        private static IrrigationController CreateController(INotifier? notifier = null)
        {
            var controller = new IrrigationController(notifier ?? new Mock<INotifier>().Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            return controller;
        }

        // ─── ScheduleProposal ─────────────────────────────────────────────────

        [Fact]
        public async Task ScheduleProposal_Valid_ReturnsMediatorResult()
        {
            var mediator = new Mock<IMediator>();
            var expected = new ScheduleProposalResponse { ProposalId = 42, Windows = new() };
            mediator.Setup(m => m.Send(It.IsAny<ScheduleProposalRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = CreateController();
            var result = await controller.ScheduleProposal(
                mediator.Object,
                new ScheduleProposalRequest { WaterSourceId = 1, TypicalDurationMinutes = 60 },
                CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Same(expected, obj.Value);
        }

        [Fact]
        public async Task ScheduleProposal_InvalidModelState_ReturnsBadRequest()
        {
            var mediator = new Mock<IMediator>();
            var controller = CreateController(new MockNotifier());
            controller.ModelState.AddModelError("WaterSourceId", "Required");

            var result = await controller.ScheduleProposal(mediator.Object, new ScheduleProposalRequest(), CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, obj.StatusCode);
            mediator.Verify(m => m.Send(It.IsAny<ScheduleProposalRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ─── UpdateProposal ───────────────────────────────────────────────────

        [Fact]
        public async Task UpdateProposal_Accept_SetsProposalIdAndReturnsResult()
        {
            var mediator = new Mock<IMediator>();
            var expected = new AcceptRejectProposalResponse { ProposalId = 10, Status = "accepted", DecidedAt = DateTime.UtcNow };
            mediator.Setup(m => m.Send(It.Is<AcceptRejectProposalRequest>(r => r.ProposalId == 10 && r.Action == "accept"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = CreateController();
            var result = await controller.UpdateProposal(
                mediator.Object,
                10,
                new AcceptRejectProposalRequest { Action = "accept" },
                CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Same(expected, obj.Value);
        }

        [Fact]
        public async Task UpdateProposal_InvalidModelState_ReturnsBadRequest()
        {
            var mediator = new Mock<IMediator>();
            var controller = CreateController(new MockNotifier());
            controller.ModelState.AddModelError("Action", "Required");

            var result = await controller.UpdateProposal(mediator.Object, 1, new AcceptRejectProposalRequest(), CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(400, obj.StatusCode);
            mediator.Verify(m => m.Send(It.IsAny<AcceptRejectProposalRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateProposal_Reject_SetsProposalIdAndReturnsResult()
        {
            var mediator = new Mock<IMediator>();
            var expected = new AcceptRejectProposalResponse { ProposalId = 5, Status = "rejected", DecidedAt = DateTime.UtcNow };
            mediator.Setup(m => m.Send(It.Is<AcceptRejectProposalRequest>(r => r.ProposalId == 5), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var controller = CreateController();
            var result = await controller.UpdateProposal(
                mediator.Object,
                5,
                new AcceptRejectProposalRequest { Action = "reject" },
                CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(200, obj.StatusCode);
            Assert.Equal("rejected", ((AcceptRejectProposalResponse)obj.Value!).Status);
        }
    }
}
