using AgripeWebAPI.Domain.Commands.Requests.WaterSources;
using AgripeWebAPI.Domain.Commands.Responses.WaterSources;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/water-sources")]
    public class WaterSourceController : MainController
    {
        public WaterSourceController(INotifier notificador) : base(notificador)
        {
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetListWaterSourceRequest(), cancellationToken);
            return CustomResponse(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetWaterSourceRequest { Id = id }, cancellationToken);
            return CustomResponse(result!);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromServices] IMediator mediator,
            [FromBody] CreateWaterSourceRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            [FromServices] IMediator mediator,
            int id,
            [FromBody] EditWaterSourceRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.Id = id;
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeleteWaterSourceRequest { Id = id }, cancellationToken);
            return CustomResponse(result);
        }
    }
}
