using StarkAgroAPI.Domain.Commands.Requests.Revenda;
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
    /// <summary>
    /// Portal do gestor de revenda. A policy só responde "é gestor de revenda?"; QUAL revenda ele
    /// gere é resolvido pelo <c>IRevendaMembershipService</c> a partir do token, nunca do request.
    /// </summary>
    [Authorize(Policy = "ResellerManager")]
    [Route("v1/revenda")]
    public class RevendaController : MainController
    {
        public RevendaController(INotifier notificador) : base(notificador)
        {
        }

        [HttpGet("me")]
        public async Task<ActionResult<RevendaResponse>> GetMyRevenda(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new GetMyRevendaRequest(), cancellationToken));
        }

        [HttpGet("members")]
        public async Task<ActionResult<List<RevendaMemberResponse>>> GetMembers(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new ListRevendaMembersRequest(), cancellationToken));
        }

        [HttpGet("seats")]
        public async Task<ActionResult<RevendaSeatsResponse>> GetSeats(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new GetMyRevendaSeatsRequest(), cancellationToken));
        }

        [HttpPost("members/invite")]
        public async Task<ActionResult<RevendaMemberResponse>> InviteMember(
            [FromServices] IMediator mediator,
            [FromBody] InviteRevendaMemberRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            return CustomResponse(await mediator.Send(command, cancellationToken));
        }

        [HttpDelete("members/{linkId}")]
        public async Task<ActionResult> RevokeMember(
            [FromServices] IMediator mediator,
            [FromRoute] int linkId,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new RevokeRevendaMemberRequest { LinkId = linkId }, cancellationToken);
            return ok ? CustomResponse(null, HttpStatusCode.NoContent) : CustomResponse(null, HttpStatusCode.BadRequest);
        }

        [HttpGet("billing")]
        public async Task<ActionResult<RevendaBillingResponse>> GetBilling(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new GetMyRevendaBillingRequest(), cancellationToken));
        }
    }
}
