using AgripeWebAPI.Domain.Commands.Requests.Irrigation;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/irrigation")]
    public class IrrigationController : MainController
    {
        public IrrigationController(INotifier notificador) : base(notificador)
        {
        }

        [HttpPost("schedule-proposal")]
        public async Task<IActionResult> ScheduleProposal(
            [FromServices] IMediator mediator,
            [FromBody] ScheduleProposalRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }

        [HttpPatch("proposals/{id:int}")]
        public async Task<IActionResult> UpdateProposal(
            [FromServices] IMediator mediator,
            int id,
            [FromBody] AcceptRejectProposalRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.ProposalId = id;
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }
    }
}
