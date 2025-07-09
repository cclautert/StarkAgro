using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
    [ApiController]
    [Route("v1/reads")]
    public class ReadsController : MainController
    {
        public ReadsController(INotifier notificador) : base(notificador)
        {
        }

        [HttpGet("GetActive")]
        public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
        {
            return Ok();
        }
        
        [HttpGet("GetAll")]
        public async Task<IAsyncEnumerable<GetReadResponse>> GetAll(
            [FromServices] IMediator mediator,
            [FromQuery] GetListReadRequest command,
            CancellationToken cancellationToken
        )
        {
            command.UserId = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }

        [HttpGet("GetAllByPivotId")]
        public async Task<IAsyncEnumerable<GetAllReadByPivotIdResponse>> GetAllByPivotId(
            [FromServices] IMediator mediator,
            [FromQuery] GetAllListReadByPivotIdRequest command,
            CancellationToken cancellationToken
        )
        {
            return await mediator.Send(command, cancellationToken);
        }

        [HttpGet("GetByPivotId")]
        public async Task<GetReadByPivotIdResponse> GetByPivotId(
            [FromServices] IMediator mediator,
            [FromQuery] GetListReadByPivotIdRequest command,
            CancellationToken cancellationToken
        )
        {
            return await mediator.Send(command, cancellationToken);
        }
                
        [HttpPost("Add")]
        public async Task<CreateReadResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateReadRequest command,
            CancellationToken cancellationToken
        )
        { 
            command.UserId = GetCurrentUserId();
            return await mediator.Send(command, cancellationToken);
        }
    }    
}
