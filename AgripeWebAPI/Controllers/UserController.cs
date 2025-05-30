using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
   [ApiController]
    [Route("v1/user")]
    public class UserController : ControllerBase
    {              
        [Route("getById")]
        [HttpGet]
        public async Task<GetUserResponse> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetUserRequest command
        )
        { 
            return await mediator.Send(command);
        }

        [Route("add")]
        [HttpPost]
        public async Task<CreateUserResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateUserRequest command
        )
        { 
            return await mediator.Send(command);
        }
    }
}
