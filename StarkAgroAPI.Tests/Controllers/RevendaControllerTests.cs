using System.Security.Claims;
using StarkAgroAPI.Controllers;
using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using StarkAgroAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace StarkAgroAPI.Tests.Controllers
{
    public class RevendaControllerTests
    {
        private static RevendaController CreateController(MockNotifier? notifier = null)
        {
            var controller = new RevendaController(notifier ?? new MockNotifier());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim("id", "42"),
                        new Claim("isResellerManager", "true")
                    }))
                }
            };
            return controller;
        }

        [Fact]
        public async Task GetMyRevenda_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetMyRevendaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RevendaResponse { Id = 7, Name = "AgroSul" });

            var result = await CreateController().GetMyRevenda(mediator.Object, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }

        [Fact]
        public async Task GetMembers_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<ListRevendaMembersRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RevendaMemberResponse> { new RevendaMemberResponse { Id = 1 } });

            var result = await CreateController().GetMembers(mediator.Object, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }

        [Fact]
        public async Task InviteMember_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<InviteRevendaMemberRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RevendaMemberResponse { Id = 1, MemberEmail = "a@b.com" });

            var result = await CreateController().InviteMember(mediator.Object,
                new InviteRevendaMemberRequest { Email = "a@b.com", Role = "Client" }, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }

        [Fact]
        public async Task InviteMember_InvalidModel_ReturnsBadRequest()
        {
            var controller = CreateController();
            controller.ModelState.AddModelError("Email", "required");

            var result = await controller.InviteMember(new Mock<IMediator>().Object,
                new InviteRevendaMemberRequest(), default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, obj.StatusCode);
        }

        [Fact]
        public async Task RevokeMember_Ok_Returns204()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<RevokeRevendaMemberRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await CreateController().RevokeMember(mediator.Object, 9, default);

            var sc = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(204, sc.StatusCode);
        }

        [Fact]
        public async Task RevokeMember_False_Returns400()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<RevokeRevendaMemberRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await CreateController().RevokeMember(mediator.Object, 9, default);

            var sc = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(400, sc.StatusCode);
        }

        [Fact]
        public async Task GetBilling_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetMyRevendaBillingRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RevendaBillingResponse { RevendaId = 7, TotalCents = 12400 });

            var result = await CreateController().GetBilling(mediator.Object, default);

            var obj = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(200, obj.StatusCode);
        }
    }
}
