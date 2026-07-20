using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StarkAgroAPI.Controllers
{
    [Authorize]
    [Route("v1/pivot")]
    public class PivotController : MainController
    {
        public PivotController(INotifier notificador) : base(notificador)
        {
        }

        [Route("getById")]
        [HttpGet]
        public async Task<ActionResult<GetPivotResponse>> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetPivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("getAll")]
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromServices] IMediator mediator,
            [FromQuery] GetListPivotByUserIdRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            List<GetPivotResponse> lstPivots = await mediator.Send(command, cancellationToken);

            return CustomResponse(lstPivots);
        }

        [Route("add")]
        [HttpPost]
        public async Task<IActionResult> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreatePivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }

        [Route("update")]
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditPivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }

        [Route("updateLimits")]
        [HttpPut]
        public async Task<ActionResult<EditPivotResponse>> UpdateLimits(
            [FromServices] IMediator mediator,
            [FromBody] EditPivotLimitsRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("delete")]
        [HttpDelete]
        public async Task<ActionResult<DeletePivotResponse>> Delete(
            [FromServices] IMediator mediator,
            [FromQuery] DeletePivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }
    }
}
