using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Net;
using System.Text.Json;

namespace StarkAgroAPI.Controllers
{
    [ApiController]
    public abstract class MainController : ControllerBase
    {
        private readonly INotifier _notificador;

        protected MainController(INotifier notificador)
        {
            _notificador = notificador;
        }

        protected bool ValidOperation()
        {
            return !_notificador.HasNotification();
        }

        protected ActionResult CustomResponse(object? result = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            if (ValidOperation())
            {
                if (result != null)
                {
                    return new ObjectResult(result)
                    {
                        StatusCode = Convert.ToInt32(statusCode),
                    };
                }
                else
                {
                    return new StatusCodeResult((int)statusCode);
                }
            }

            // Respect the caller's intended status code; fall back to 400 only when no
            // specific error code was provided (i.e. the caller used the default OK).
            var errorStatus = statusCode == HttpStatusCode.OK ? HttpStatusCode.BadRequest : statusCode;
            return new ObjectResult(new
            {
                errors = _notificador.getNotifications().Select(n => n.Mensagem)
            })
            {
                StatusCode = (int)errorStatus
            };
        }

        protected int GetCurrentUserId()
        {
            var userId = getUserIdFromClaim();
            if (!string.IsNullOrEmpty(userId))
            {
                return Convert.ToInt32(userId);
            }
            return 0;
        }

        protected bool GetCurrentUserIsAdmin()
            => User?.Claims?.FirstOrDefault(c => c.Type == "isAdmin")?.Value == "true";

        private string getUserIdFromClaim()
        {
            var userClaim = User?.Claims?.FirstOrDefault(c => c.Type == "id")?.Value;

            if (!string.IsNullOrEmpty(userClaim))
            {
                return userClaim;
            }

            return string.Empty;
        }

        protected ActionResult CustomResponse(ModelStateDictionary modelState)
        {
            if (!modelState.IsValid) NotifyErrorModelInvalid(modelState);
            return CustomResponse();
        }

        protected void NotifyErrorModelInvalid(ModelStateDictionary modelState)
        {
            var erros = modelState.Values.SelectMany(e => e.Errors);
            foreach (var erro in erros)
            {
                var errorMsg = erro.Exception == null ? erro.ErrorMessage : erro.Exception.Message;
                NotifyError(errorMsg);
            }
        }

        protected void NotifyError(string mensagem)
        {
            _notificador.Handle(new Notification(mensagem));
        }
    }
}
