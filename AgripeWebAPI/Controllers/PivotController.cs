using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
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
            [FromQuery] GetPivotRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("getAllbyUserId")]
        [HttpGet]
        public async Task<IList<GetPivotResponse>> getAllbyUserId(
            [FromServices] IMediator mediator,
            [FromQuery] GetListPivotByUserIdRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("add")]
        [HttpPost]
        public async Task<CreatePivotResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreatePivotRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("update")]
        [HttpPut]
        public async Task<EditPivotResponse> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditPivotRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }
    }
}
