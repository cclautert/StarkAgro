using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
   [ApiController]
    [Route("v1/pivot")]
    public class PivotController : ControllerBase
    {              
        [Route("getById")]
        [HttpGet]
        public async Task<GetPivotResponse> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetPivotRequest command
        )
        { 
            return await mediator.Send(command);
        }

        [Route("getAllbyUserId")]
        [HttpGet]
        public async Task<IList<GetPivotResponse>> getAllbyUserId(
            [FromServices] IMediator mediator,
            [FromQuery] GetListPivotByUserIdRequest command
        )
        { 
            return await mediator.Send(command);
        }

        [Route("add")]
        [HttpPost]
        public async Task<CreatePivotResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreatePivotRequest command
        )
        { 
            return await mediator.Send(command);
        }
    }
}
