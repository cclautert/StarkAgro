using AgripeWebAPI.Domain.Commands.Requests;
using AgripeWebAPI.Domain.Commands.Responses;
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
