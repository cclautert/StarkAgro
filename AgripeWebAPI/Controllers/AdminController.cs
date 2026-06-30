using AgripeWebAPI.Domain.Commands.Requests.Admin;
using AgripeWebAPI.Domain.Commands.Responses.Admin;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/admin")]
    public class AdminController : MainController
    {
        public AdminController(INotifier notificador) : base(notificador)
        {
        }

        [HttpGet("users")]
        public async Task<ActionResult<List<AdminUserResponse>>> GetAllUsers(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            return CustomResponse(await mediator.Send(new GetAllUsersRequest(), cancellationToken));
        }

        [HttpPost("users")]
        public async Task<ActionResult<AdminUserResponse>> CreateUser(
            [FromServices] IMediator mediator,
            [FromBody] AdminCreateUserRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            return CustomResponse(await mediator.Send(command, cancellationToken), HttpStatusCode.Created);
        }

        [HttpPut("users/{id}")]
        public async Task<ActionResult<AdminUserResponse>> EditUser(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            [FromBody] AdminEditUserRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            command.Id = id;
            return CustomResponse(await mediator.Send(command, cancellationToken));
        }

        [HttpPut("users/{id}/toggle-active")]
        public async Task<ActionResult<AdminUserResponse>> ToggleActive(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            [FromBody] AdminToggleUserActiveRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            command.Id = id;
            return CustomResponse(await mediator.Send(command, cancellationToken));
        }

        [HttpDelete("users/{id}")]
        public async Task<ActionResult> DeleteUser(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            await mediator.Send(new AdminDeleteUserRequest { Id = id }, cancellationToken);
            return CustomResponse(null, HttpStatusCode.NoContent);
        }

        [HttpGet("ai-settings")]
        public async Task<ActionResult<AdminAiSettingsResponse>> GetAiSettings(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            return CustomResponse(await mediator.Send(new GetPlatformAiSettingsRequest(), cancellationToken));
        }

        [HttpPut("ai-settings")]
        public async Task<ActionResult> UpdateAiSettings(
            [FromServices] IMediator mediator,
            [FromBody] UpdatePlatformAiSettingsRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            await mediator.Send(command, cancellationToken);
            return CustomResponse(null, HttpStatusCode.NoContent);
        }
    }
}
