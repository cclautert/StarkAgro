using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace StarkAgroAPI.Controllers
{
    [Authorize]
    [Route("v1/areas")]
    public class NdviController : MainController
    {
        public NdviController(INotifier notificador) : base(notificador)
        {
        }

        [HttpGet]
        public async Task<ActionResult<List<MonitoredAreaResponse>>> List(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new ListMonitoredAreasRequest(), cancellationToken));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MonitoredAreaResponse>> Get(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new GetMonitoredAreaRequest { Id = id }, cancellationToken));
        }

        [HttpGet("{id:int}/trend")]
        public async Task<ActionResult<NdviTrendResponse>> Trend(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new GetNdviTrendRequest { AreaId = id }, cancellationToken));
        }

        [HttpPost]
        public async Task<ActionResult<MonitoredAreaResponse>> Create(
            [FromServices] IMediator mediator,
            [FromBody] CreateMonitoredAreaRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            return CustomResponse(await mediator.Send(command, cancellationToken), HttpStatusCode.Created);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<MonitoredAreaResponse>> Edit(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            [FromBody] EditMonitoredAreaRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);
            command.Id = id;
            return CustomResponse(await mediator.Send(command, cancellationToken));
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(
            [FromServices] IMediator mediator,
            [FromRoute] int id,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new DeleteMonitoredAreaRequest { Id = id }, cancellationToken);
            return ok ? CustomResponse(null, HttpStatusCode.NoContent) : CustomResponse(null, HttpStatusCode.BadRequest);
        }
    }
}
