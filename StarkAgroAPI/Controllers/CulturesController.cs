using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StarkAgroAPI.Controllers
{
    /// <summary>
    /// Lista de culturas para os seletores (área, perfil de adubação, diagnóstico). Leitura para
    /// <b>qualquer usuário autenticado</b> — o CRUD é do admin (<c>v1/admin/cultures</c>).
    /// </summary>
    [Authorize]
    [Route("v1/cultures")]
    public class CulturesController : MainController
    {
        public CulturesController(INotifier notificador) : base(notificador)
        {
        }

        [HttpGet]
        public async Task<ActionResult<List<string>>> List(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            return CustomResponse(await mediator.Send(new GetCulturesRequest(), cancellationToken));
        }
    }
}
