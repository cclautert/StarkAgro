using AgripeWebAPI.Domain.Commands.Requests.Read;
using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Commands.Responses.Read;
using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
    [ApiController]
    [Route("v1/reads")]
    public class ReadsController : ControllerBase
    {
        [Route("getAll")]
        [HttpGet]
        public async Task<IList<GetReadResponse>> Get(
            [FromServices] IMediator mediator,
            [FromQuery] GetListReadRequest command
        )
        { 
            return await mediator.Send(command);
        }

        [Route("add")]
        [HttpPost]
        public async Task<CreateReadResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateReadRequest command
        )
        { 
            return await mediator.Send(command);
        }
    }    
}
