using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgripeWebAPI.Controllers
{
    [Route("v1/Auth")]
    public class AuthController: MainController
    {        
        public AuthController(INotifier notificador) : base(notificador)
        {
        }

        [HttpPost("LogIn")]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<UserTokenResponse>> LogIn(
            [FromServices] IMediator mediator,
            [FromBody] UserTokenRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await mediator.Send(command, cancellationToken);

            return result.ErrorCode switch
            {
                LoginErrorCode.AccountInactive =>
                    StatusCode(403, new { errors = new[] { "Conta desativada." } }),
                LoginErrorCode.TooManyAttempts =>
                    StatusCode(429, new { errors = new[] { "Muitas tentativas. Tente novamente em alguns minutos." } }),
                LoginErrorCode.InvalidCredentials =>
                    StatusCode(401, new { errors = new[] { "Email ou senha inválidos." } }),
                _ => Ok(result)
            };
        }

        /// <summary>
        /// OAuth 2.0: exchange authorization code (e.g. from Google) for a JWT.
        /// Body: { "provider": "Google", "code": "...", "redirectUri": "https://localhost:4200/login/callback" }
        /// </summary>
        [HttpPost("external-login")]
        public async Task<ActionResult<UserTokenResponse>> ExternalLogin(
            [FromServices] IMediator mediator,
            [FromBody] ExternalLoginRequest command,
            CancellationToken cancellationToken)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Code))
            {
                NotifyError("Código de autorização inválido.");
                return CustomResponse(null, System.Net.HttpStatusCode.BadRequest);
            }

            var result = await mediator.Send(command, cancellationToken);

            if (result?.ErrorCode == LoginErrorCode.AccountInactive)
                return StatusCode(403, new { errors = new[] { "Conta desativada." } });

            if (result == null || result.ErrorCode != LoginErrorCode.None)
            {
                NotifyError("Falha no login com o provedor externo.");
                return CustomResponse(null, System.Net.HttpStatusCode.Unauthorized);
            }

            return Ok(result);
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
