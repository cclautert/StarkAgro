using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AgripeWebAPI.Controllers
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
    }
}
