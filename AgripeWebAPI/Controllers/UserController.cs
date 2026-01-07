using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/user")]
    public class UserController : MainController
    { 
        public UserController(INotifier notificador) : base(notificador)
        {
        }

        [Route("getById")]
        [HttpGet]
        public async Task<ActionResult<GetUserResponse>> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.CurrentUserId = GetCurrentUserId();
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result);
        }

        [Route("add")]
        [HttpPost]
        public async Task<ActionResult<CreateUserResponse>> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreateUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("update")]
        [HttpPut]
        public async Task<ActionResult<EditUserResponse>> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.CurrentUserId = GetCurrentUserId();
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result);
        }
    }
}
