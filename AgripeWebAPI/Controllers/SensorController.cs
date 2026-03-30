using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/sensor")]
    public class SensorController : MainController
    {   
        public SensorController(INotifier notificador) : base(notificador)
        {
        }

        [Route("getById")]
        [HttpGet]
        public async Task<ActionResult<GetSensorResponse>> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetSensorRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("getAll")]
        [HttpGet]
        public async Task<IList<GetSensorResponse>> getAll(
            [FromServices] IMediator mediator,
            [FromQuery] GetListSensorByUserIdRequest command,
            CancellationToken cancellationToken
        )
        { 
            //if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }

        [Route("getAllByPivotId")]
        [HttpGet]
        public async Task<IList<GetSensorResponse>> getAllByPivotId(
            [FromServices] IMediator mediator,
            [FromQuery] GetListSensorRequest command,
            CancellationToken cancellationToken
        )
        { 
            //if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }

        [Route("add")]
        [HttpPost]
        public async Task<ActionResult<CreateSensorResponse>> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateSensorRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }

        [Route("update")]
        [HttpPut]
        public async Task<ActionResult<EditSensorResponse>> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditSensorRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }
    }
}
