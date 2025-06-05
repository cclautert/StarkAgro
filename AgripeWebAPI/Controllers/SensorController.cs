using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
   [ApiController]
    [Route("v1/sensor")]
    public class SensorController : ControllerBase
    {              
        [Route("getById")]
        [HttpGet]
        public async Task<GetSensorResponse> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetSensorRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("getAllbyUserId")]
        [HttpGet]
        public async Task<IList<GetSensorResponse>> getAllbyUserId(
            [FromServices] IMediator mediator,
            [FromQuery] GetListSensorByUserIdRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("add")]
        [HttpPost]
        public async Task<CreateSensorResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateSensorRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("update")]
        [HttpPut]
        public async Task<EditSensorResponse> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditSensorRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }
    }
}
