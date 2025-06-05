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
            [FromQuery] GetUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("add")]
        [HttpPost]
        public async Task<CreateUserResponse> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }

        [Route("update")]
        [HttpPut]
        public async Task<EditUserResponse> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            return await mediator.Send(command, cancellationToken);
        }
    }
}
