using StarkAgroAPI.Domain.Commands.Requests.Agronomist;
using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Commands.Responses.Users;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace StarkAgroAPI.Controllers
{
    [Authorize]
    [Route("v1/user")]
    public class UserController : MainController
    { 
        public UserController(INotifier notificador) : base(notificador)
        {
        }

        [Route("getById")]
        [HttpGet]
        public async Task<ActionResult<GetUserResponse>> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("add")]
        [HttpPost]
        public async Task<ActionResult<CreateUserResponse>> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("updateLimits")]
        [HttpPut]
        public async Task<ActionResult<EditUserResponse>> UpdateLimits(
            [FromServices] IMediator mediator,
            [FromBody] EditUserLimitsRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.Id = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }

        [Route("update")]
        [HttpPut]
        public async Task<ActionResult<EditUserResponse>> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditUserRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("pushToken")]
        [HttpPut]
        public async Task<ActionResult> RegisterPushToken(
            [FromServices] IMediator mediator,
            [FromBody] RegisterExpoPushTokenRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            await mediator.Send(command, cancellationToken);
            return CustomResponse(null, HttpStatusCode.NoContent);
        }

        [Route("alerts")]
        [HttpGet]
        public async Task<IList<UserAlertResponse>> GetAlerts(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            return await mediator.Send(new GetUserAlertsRequest(), cancellationToken);
        }

        [Route("alerts/mark-read")]
        [HttpPost]
        public async Task<ActionResult> MarkAlertsRead(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            await mediator.Send(new MarkAlertsReadRequest(), cancellationToken);
            return CustomResponse(null, HttpStatusCode.NoContent);
        }

        [Route("webPushSubscription")]
        [HttpPut]
        public async Task<ActionResult> RegisterWebPushSubscription(
            [FromServices] IMediator mediator,
            [FromBody] RegisterWebPushSubscriptionRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            await mediator.Send(command, cancellationToken);
            return CustomResponse(null, HttpStatusCode.NoContent);
        }

        // ── Vínculo com o agrônomo (lado do produtor) ─────────────────────────

        [Route("agronomist-invites")]
        [HttpGet]
        public async Task<IActionResult> GetAgronomistInvites(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMyAgronomistInvitesRequest(), cancellationToken);
            return CustomResponse(result);
        }

        [Route("agronomist-invites/{id:int}/accept")]
        [HttpPost]
        public async Task<IActionResult> AcceptAgronomistInvite(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new AcceptAgronomistInviteRequest { InviteId = id }, cancellationToken);
            return ok ? CustomResponse() : CustomResponse(null, HttpStatusCode.BadRequest);
        }

        [Route("agronomist-invites/{id:int}/decline")]
        [HttpPost]
        public async Task<IActionResult> DeclineAgronomistInvite(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new DeclineAgronomistInviteRequest { InviteId = id }, cancellationToken);
            return ok ? CustomResponse() : CustomResponse(null, HttpStatusCode.BadRequest);
        }

        /// <summary>O produtor demite o agrônomo. Direito dele, sem intermediários.</summary>
        [Route("agronomist-link")]
        [HttpDelete]
        public async Task<IActionResult> RevokeMyAgronomist(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new RevokeMyAgronomistRequest(), cancellationToken);
            return ok ? CustomResponse(null, HttpStatusCode.NoContent) : CustomResponse(null, HttpStatusCode.BadRequest);
        }
    }
}
