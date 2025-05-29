using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
   [ApiController]
    [Route("v1/sensor")]
    public class SensorController : ControllerBase
    {              
        [Route("get")]
        [HttpGet]
        public async Task<GetSensorResponse> Get(
            [FromServices] IMediator mediator,
            [FromQuery] GetSensorRequest command
        )
        { 
            return await mediator.Send(command);
        }

        [Route("getAll")]
        [HttpGet]
        public async Task<IList<GetSensorResponse>> Get(
            [FromServices] IMediator mediator,
            [FromQuery] GetListSensorByUserIdRequest command
        )
        { 
            return await mediator.Send(command);
        }

        [Route("add")]
        [HttpPost]
        public async Task<CreateSensorResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateSensorRequest command
        )
        { 
            return await mediator.Send(command);
        }
    }
}
