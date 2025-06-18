using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Xml.Linq;

namespace AgripeWebAPI.Controllers
{
    [Route("v1/Auth")]
    public class AuthController: MainController
    {        
        public AuthController(INotifier notificador) : base(notificador)
        {
        }

        [HttpPost("LogIn")]
        public async Task<ActionResult<UserTokenResponse>> LogIn(
            [FromServices] IMediator mediator,
            [FromBody] UserTokenRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }
        
        [Route("addUser")]
        [HttpPost]
        public async Task<ActionResult<CreateUserResponse>> AddUser(
            [FromServices] IMediator mediator,
            [FromBody] CreateUserRequest command,
            CancellationToken cancellationToken
        )
        { 
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }
    }
}
