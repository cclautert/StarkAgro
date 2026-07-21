using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace StarkAgroAPI.Controllers
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

        [HttpGet("diagnosis-plans")]
        public async Task<ActionResult<List<DiagnosisPlanResponse>>> GetDiagnosisPlans(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            return CustomResponse(await mediator.Send(new GetDiagnosisPlansRequest(), cancellationToken));
        }

        [HttpPost("diagnosis-plans")]
        public async Task<ActionResult<DiagnosisPlanResponse>> CreateDiagnosisPlan(
            [FromServices] IMediator mediator,
            [FromBody] CreateDiagnosisPlanRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            return CustomResponse(await mediator.Send(command, cancellationToken), HttpStatusCode.Created);
        }

        [HttpPut("diagnosis-plans/{id}")]
        public async Task<ActionResult<DiagnosisPlanResponse>> UpdateDiagnosisPlan(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            [FromBody] UpdateDiagnosisPlanRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            command.Id = id;
            return CustomResponse(await mediator.Send(command, cancellationToken));
        }

        [HttpDelete("diagnosis-plans/{id}")]
        public async Task<ActionResult> DeleteDiagnosisPlan(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            await mediator.Send(new DeleteDiagnosisPlanRequest { Id = id }, cancellationToken);
            return CustomResponse(null, HttpStatusCode.NoContent);
        }

        [HttpGet("revendas")]
        public async Task<ActionResult<List<RevendaResponse>>> GetRevendas(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            return CustomResponse(await mediator.Send(new GetRevendasRequest(), cancellationToken));
        }

        [HttpPost("revendas")]
        public async Task<ActionResult<RevendaResponse>> CreateRevenda(
            [FromServices] IMediator mediator,
            [FromBody] CreateRevendaRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            return CustomResponse(await mediator.Send(command, cancellationToken), HttpStatusCode.Created);
        }

        [HttpPut("revendas/{id}")]
        public async Task<ActionResult<RevendaResponse>> UpdateRevenda(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            [FromBody] UpdateRevendaRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            command.Id = id;
            return CustomResponse(await mediator.Send(command, cancellationToken));
        }

        [HttpPost("revendas/{id}/manager")]
        public async Task<ActionResult<RevendaResponse>> AssignRevendaManager(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            [FromBody] AssignRevendaManagerRequest command,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            command.RevendaId = id;
            return CustomResponse(await mediator.Send(command, cancellationToken));
        }

        [HttpGet("revendas/{id}/billing")]
        public async Task<ActionResult<RevendaBillingResponse>> GetRevendaBilling(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            if (!GetCurrentUserIsAdmin()) return StatusCode(403, new { errors = new[] { "Acesso negado." } });
            return CustomResponse(await mediator.Send(new GetRevendaBillingRequest { RevendaId = id }, cancellationToken));
        }
    }
}
