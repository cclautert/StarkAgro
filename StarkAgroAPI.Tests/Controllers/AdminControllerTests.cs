using System.Security.Claims;
using StarkAgroAPI.Controllers;
using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Tests.Mocks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace StarkAgroAPI.Tests.Controllers
{
    public class AdminControllerTests
    {
        private static AdminController CreateController(bool isAdmin, MockNotifier? notifier = null)
        {
            notifier ??= new MockNotifier();
            var controller = new AdminController(notifier);
            var claims = new List<Claim> { new Claim("id", "1") };
            if (isAdmin) claims.Add(new Claim("isAdmin", "true"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims))
                }
            };
            return controller;
        }

        private static ObjectResult AssertObjectResult(IActionResult result, int statusCode)
        {
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(statusCode, obj.StatusCode);
            return obj;
        }

        // ─── GetAllUsers ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsers_AsAdmin_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            var users = new List<AdminUserResponse> { new AdminUserResponse { Id = 1, Name = "Alice" } };
            mediator.Setup(m => m.Send(It.IsAny<GetAllUsersRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            var result = await CreateController(true).GetAllUsers(mediator.Object, default);

            var obj = AssertObjectResult(result.Result!, 200);
            var list = Assert.IsType<List<AdminUserResponse>>(obj.Value);
            Assert.Single(list);
        }

        [Fact]
        public async Task GetAllUsers_NonAdmin_Returns403()
        {
            var mediator = new Mock<IMediator>();
            var result = await CreateController(false).GetAllUsers(mediator.Object, default);

            AssertObjectResult(result.Result!, 403);
        }

        // ─── CreateUser ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateUser_AsAdmin_ReturnsCreated()
        {
            var mediator = new Mock<IMediator>();
            var response = new AdminUserResponse { Id = 10, Name = "Bob", Email = "bob@example.com" };
            mediator.Setup(m => m.Send(It.IsAny<AdminCreateUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var result = await CreateController(true).CreateUser(mediator.Object,
                new AdminCreateUserRequest { Name = "Bob", Email = "bob@example.com", Password = "Pass@1" }, default);

            AssertObjectResult(result.Result!, 201);
        }

        [Fact]
        public async Task CreateUser_NonAdmin_Returns403()
        {
            var mediator = new Mock<IMediator>();
            var result = await CreateController(false).CreateUser(mediator.Object,
                new AdminCreateUserRequest { Name = "Bob", Email = "bob@example.com", Password = "Pass@1" }, default);

            AssertObjectResult(result.Result!, 403);
        }

        [Fact]
        public async Task CreateUser_InvalidModel_ReturnsBadRequest()
        {
            var mediator = new Mock<IMediator>();
            var controller = CreateController(true);
            controller.ModelState.AddModelError("Name", "required");

            var result = await controller.CreateUser(mediator.Object, new AdminCreateUserRequest(), default);

            AssertObjectResult(result.Result!, 400);
        }

        // ─── EditUser ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task EditUser_AsAdmin_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            var response = new AdminUserResponse { Id = 1, Name = "Updated" };
            mediator.Setup(m => m.Send(It.IsAny<AdminEditUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var result = await CreateController(true).EditUser(mediator.Object, 1,
                new AdminEditUserRequest { Name = "Updated", Email = "u@u.com" }, default);

            var obj = AssertObjectResult(result.Result!, 200);
            Assert.Equal("Updated", (obj.Value as AdminUserResponse)?.Name);
        }

        [Fact]
        public async Task EditUser_NonAdmin_Returns403()
        {
            var mediator = new Mock<IMediator>();
            var result = await CreateController(false).EditUser(mediator.Object, 1,
                new AdminEditUserRequest { Name = "X", Email = "x@x.com" }, default);

            AssertObjectResult(result.Result!, 403);
        }

        // ─── ToggleActive ────────────────────────────────────────────────────────────

        [Fact]
        public async Task ToggleActive_AsAdmin_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<AdminToggleUserActiveRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AdminUserResponse { Id = 1, Active = false });

            var result = await CreateController(true).ToggleActive(mediator.Object, 1,
                new AdminToggleUserActiveRequest { Active = false }, default);

            var obj = AssertObjectResult(result.Result!, 200);
            Assert.False((obj.Value as AdminUserResponse)?.Active);
        }

        [Fact]
        public async Task ToggleActive_NonAdmin_Returns403()
        {
            var mediator = new Mock<IMediator>();
            var result = await CreateController(false).ToggleActive(mediator.Object, 1,
                new AdminToggleUserActiveRequest { Active = false }, default);

            AssertObjectResult(result.Result!, 403);
        }

        // ─── DeleteUser ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteUser_AsAdmin_Returns204()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<AdminDeleteUserRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await CreateController(true).DeleteUser(mediator.Object, 1, default);

            var sc = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(204, sc.StatusCode);
        }

        [Fact]
        public async Task DeleteUser_NonAdmin_Returns403()
        {
            var mediator = new Mock<IMediator>();
            var result = await CreateController(false).DeleteUser(mediator.Object, 1, default);

            AssertObjectResult(result, 403);
        }

        // ─── GetAiSettings ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAiSettings_AsAdmin_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            var settings = new AdminAiSettingsResponse { ActiveProvider = "gemini" };
            mediator.Setup(m => m.Send(It.IsAny<GetPlatformAiSettingsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            var result = await CreateController(true).GetAiSettings(mediator.Object, default);

            var obj = AssertObjectResult(result.Result!, 200);
            Assert.Equal("gemini", (obj.Value as AdminAiSettingsResponse)?.ActiveProvider);
        }

        [Fact]
        public async Task GetAiSettings_NonAdmin_Returns403()
        {
            var mediator = new Mock<IMediator>();
            var result = await CreateController(false).GetAiSettings(mediator.Object, default);

            AssertObjectResult(result.Result!, 403);
        }

        // ─── UpdateAiSettings ────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAiSettings_AsAdmin_Returns204()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<UpdatePlatformAiSettingsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await CreateController(true).UpdateAiSettings(mediator.Object,
                new UpdatePlatformAiSettingsRequest { ActiveProvider = "openai" }, default);

            var sc = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(204, sc.StatusCode);
        }

        [Fact]
        public async Task UpdateAiSettings_NonAdmin_Returns403()
        {
            var mediator = new Mock<IMediator>();
            var result = await CreateController(false).UpdateAiSettings(mediator.Object,
                new UpdatePlatformAiSettingsRequest { ActiveProvider = "openai" }, default);

            AssertObjectResult(result, 403);
        }

        [Fact]
        public async Task UpdateAiSettings_InvalidModel_ReturnsBadRequest()
        {
            var mediator = new Mock<IMediator>();
            var controller = CreateController(true);
            controller.ModelState.AddModelError("ActiveProvider", "required");

            var result = await controller.UpdateAiSettings(mediator.Object,
                new UpdatePlatformAiSettingsRequest(), default);

            AssertObjectResult(result, 400);
        }

        // ─── Revendas ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetRevendas_AsAdmin_ReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetRevendasRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RevendaResponse> { new RevendaResponse { Id = 1, Name = "AgroSul" } });

            var result = await CreateController(true).GetRevendas(mediator.Object, default);

            var obj = AssertObjectResult(result.Result!, 200);
            Assert.Single(Assert.IsType<List<RevendaResponse>>(obj.Value));
        }

        [Fact]
        public async Task GetRevendas_NonAdmin_Returns403()
        {
            var result = await CreateController(false).GetRevendas(new Mock<IMediator>().Object, default);
            AssertObjectResult(result.Result!, 403);
        }

        [Fact]
        public async Task CreateRevenda_AsAdmin_ReturnsCreated()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<CreateRevendaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RevendaResponse { Id = 5, Name = "AgroSul" });

            var result = await CreateController(true).CreateRevenda(mediator.Object,
                new CreateRevendaRequest { Name = "AgroSul" }, default);

            AssertObjectResult(result.Result!, 201);
        }

        [Fact]
        public async Task CreateRevenda_NonAdmin_Returns403()
        {
            var result = await CreateController(false).CreateRevenda(new Mock<IMediator>().Object,
                new CreateRevendaRequest { Name = "AgroSul" }, default);
            AssertObjectResult(result.Result!, 403);
        }

        [Fact]
        public async Task CreateRevenda_InvalidModel_ReturnsBadRequest()
        {
            var controller = CreateController(true);
            controller.ModelState.AddModelError("Name", "required");

            var result = await controller.CreateRevenda(new Mock<IMediator>().Object,
                new CreateRevendaRequest(), default);

            AssertObjectResult(result.Result!, 400);
        }

        [Fact]
        public async Task UpdateRevenda_AsAdmin_SetsIdAndReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<UpdateRevendaRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RevendaResponse { Id = 7, Name = "AgroSul" });

            var command = new UpdateRevendaRequest { Name = "AgroSul" };
            var result = await CreateController(true).UpdateRevenda(mediator.Object, 7, command, default);

            AssertObjectResult(result.Result!, 200);
            Assert.Equal(7, command.Id);
        }

        [Fact]
        public async Task UpdateRevenda_NonAdmin_Returns403()
        {
            var result = await CreateController(false).UpdateRevenda(new Mock<IMediator>().Object, 1,
                new UpdateRevendaRequest { Name = "AgroSul" }, default);
            AssertObjectResult(result.Result!, 403);
        }

        [Fact]
        public async Task AssignRevendaManager_AsAdmin_SetsRevendaIdAndReturnsOk()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<AssignRevendaManagerRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RevendaResponse { Id = 3, Name = "AgroSul" });

            var command = new AssignRevendaManagerRequest { UserId = 9 };
            var result = await CreateController(true).AssignRevendaManager(mediator.Object, 3, command, default);

            AssertObjectResult(result.Result!, 200);
            Assert.Equal(3, command.RevendaId);
        }

        [Fact]
        public async Task AssignRevendaManager_NonAdmin_Returns403()
        {
            var result = await CreateController(false).AssignRevendaManager(new Mock<IMediator>().Object, 3,
                new AssignRevendaManagerRequest { UserId = 9 }, default);
            AssertObjectResult(result.Result!, 403);
        }
    }
}
