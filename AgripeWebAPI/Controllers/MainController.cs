using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Net;
using System.Text.Json;

namespace AgripeWebAPI.Controllers
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

        protected ActionResult CustomResponse(object result = null, HttpStatusCode statusCode = HttpStatusCode.OK)
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
                    return new StatusCodeResult((int)HttpStatusCode.NotFound);
                }
            }

            return BadRequest(new
            {
                errors = _notificador.getNotifications().Select(n => n.Mensagem)
            });
        }

        protected int GetCurrentUserId()
        {
            var userClaim = getUserClaim();
            if (!string.IsNullOrEmpty(userClaim))
            {
                using (JsonDocument doc = JsonDocument.Parse(userClaim))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("userId", out JsonElement attuidElement))
                    {
                        int userId = attuidElement.GetString() != null ? int.Parse(attuidElement.GetString()) : 0;
                        return userId;
                    }
                }
            }
            return 0;
        }

        private string getUserClaim()
        {
            var userClaim = User?.Claims?.FirstOrDefault(c => c.Type == "User")?.Value;
            
            if (!string.IsNullOrEmpty(userClaim))
            {
                return userClaim;
            }

            return string.Empty;
        }

        protected int? GetCurrentTeamId()
        {
            var userClaim = getUserClaim();
            if (!string.IsNullOrEmpty(userClaim))
            {
                using (JsonDocument doc = JsonDocument.Parse(userClaim))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("teamId", out JsonElement teamIdElement))
                    {
                        var teamIdElementString = teamIdElement.GetString();
                        if (!string.IsNullOrEmpty(teamIdElementString) && int.TryParse(teamIdElementString, out int teamId))
                        {
                            return teamId;
                        }
                    }
                }
            }
            return null;
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
