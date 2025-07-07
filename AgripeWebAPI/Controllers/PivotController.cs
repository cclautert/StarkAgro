using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
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
            IAsyncEnumerable<GetPivotResponse> lstPivots = await mediator.Send(command, cancellationToken);
            
            return CustomResponse(lstPivots);
        }

        [Route("add")]
        [HttpPost]
        public async Task<ActionResult<CreatePivotResponse>> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreatePivotRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }

        [Route("update")]
        [HttpPut]
        public async Task<ActionResult<EditPivotResponse>> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditPivotRequest command,
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
