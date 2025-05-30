using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
    [ApiController]
    [Route("v1/reads")]
    public class ReadsController : ControllerBase
    {
        [HttpGet("GetActive")]
        public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
        {
            return Ok();
        }
        
        [HttpGet("GetAll")]
        public async Task<IList<GetReadResponse>> GetAll(
            [FromServices] IMediator mediator,
            [FromQuery] GetListReadRequest command
        )
        { 
            return await mediator.Send(command);
        }
                
        [HttpPost("Add")]
        public async Task<CreateReadResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateReadRequest command
        )
        { 
            try
            {
                return await mediator.Send(command);
            }
            catch(Exception ex){
                // Log the exception (not implemented here)
                // Return a bad request with the error message
                return new CreateReadResponse { Id = -1, Content = ex.Message }; // Indicating an error occurred
            }
        }
    }    
}
