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

        protected string GetCurrentAttuid()
        {
            var userClaim = getUserClaim();
            if (!string.IsNullOrEmpty(userClaim))
            {
                using (JsonDocument doc = JsonDocument.Parse(userClaim))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("attuid", out JsonElement attuidElement))
                    {
                        string attuid = attuidElement.GetString();
                        return attuid;
                    }
                }
            }
            return string.Empty;
        }

        private string getUserClaim()
        {
            var realUserClaim = User?.Claims?.FirstOrDefault(c => c.Type == "realUser")?.Value;
            var effectiveUserClaim = User?.Claims?.FirstOrDefault(c => c.Type == "effectiveUser")?.Value;
            if (!string.IsNullOrEmpty(effectiveUserClaim))
            {
                return effectiveUserClaim;
            }
            if (!string.IsNullOrEmpty(realUserClaim))
            {
                return realUserClaim;
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

        protected int? GetCurrentCenterNameId()
        {
            var userClaim = getUserClaim();
            if (!string.IsNullOrEmpty(userClaim))
            {
                using (JsonDocument doc = JsonDocument.Parse(userClaim))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("sfmCenterNameId", out JsonElement sfmCenterNameIdElement))
                    {
                        var sfmCenterNameIdString = sfmCenterNameIdElement.GetString();
                        if (!string.IsNullOrEmpty(sfmCenterNameIdString) && int.TryParse(sfmCenterNameIdString, out int sfmCenterNameId))
                        {
                            return sfmCenterNameId;
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
