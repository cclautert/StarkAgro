using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/push")]
    public class PushController : MainController
    {
        private readonly VapidSettings _vapid;

        public PushController(INotifier notificador, IOptions<VapidSettings> vapidSettings)
            : base(notificador)
        {
            _vapid = vapidSettings?.Value ?? throw new ArgumentNullException(nameof(vapidSettings));
        }

        [Route("vapid-public-key")]
        [HttpGet]
        public ActionResult<string> GetVapidPublicKey()
        {
            return CustomResponse(_vapid.PublicKey);
        }
    }
}
