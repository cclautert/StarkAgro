using AgripeWebAPI.Domain.Commands.Requests.Agronomist;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AgripeWebAPI.Controllers
{
    /// <summary>
    /// Área do agrônomo. A policy responde "é um agrônomo?"; <b>de quem</b> ele pode ler é
    /// decidido documento a documento pelo <c>IDiagnosisAccessService</c>, nos handlers.
    /// </summary>
    [Authorize(Policy = "Agronomist")]
    [Route("v1/agronomist")]
    public class AgronomistController : MainController
    {
        public AgronomistController(INotifier notificador) : base(notificador)
        {
        }

        [HttpGet("queue")]
        public async Task<IActionResult> GetQueue(
            [FromServices] IMediator mediator,
            [FromQuery] string? status,
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageIndex = 0,
            CancellationToken cancellationToken = default)
        {
            var result = await mediator.Send(
                new GetAgronomistQueueRequest { Status = status, PageSize = pageSize, PageIndex = pageIndex },
                cancellationToken);

            return CustomResponse(result);
        }

        [HttpGet("diagnosis/{id:int}")]
        public async Task<IActionResult> GetDiagnosis(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetAgronomistDiagnosisRequest { Id = id }, cancellationToken);
            if (result is null) return CustomResponse(null, HttpStatusCode.NotFound);

            return CustomResponse(result);
        }

        [HttpGet("diagnosis/{id:int}/image")]
        public async Task<IActionResult> GetDiagnosisImage(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetAgronomistDiagnosisImageRequest { Id = id }, cancellationToken);
            if (result is null) return NotFound();

            Response.Headers.CacheControl = "private, max-age=3600";
            return File(result.Content, result.ContentType);
        }

        /// <summary>PDF do laudo — mesma regra de acesso do detalhe.</summary>
        [HttpGet("diagnosis/{id:int}/pdf")]
        public async Task<IActionResult> GetDiagnosisPdf(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(
                new Domain.Commands.Requests.Diagnosis.GetDiagnosisPdfRequest { Id = id }, cancellationToken);

            if (result is null) return NotFound();

            return File(result.Content, "application/pdf", result.FileName);
        }

        [HttpPost("diagnosis/{id:int}/claim")]
        public async Task<IActionResult> Claim(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new ClaimDiagnosisRequest { Id = id }, cancellationToken);
            return ok ? CustomResponse() : CustomResponse(null, HttpStatusCode.BadRequest);
        }

        [HttpPut("diagnosis/{id:int}/review")]
        public async Task<IActionResult> Review(
            [FromServices] IMediator mediator,
            int id,
            [FromBody] ReviewDiagnosisRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.Id = id;
            var ok = await mediator.Send(command, cancellationToken);
            return ok ? CustomResponse() : CustomResponse(null, HttpStatusCode.BadRequest);
        }

        [HttpPost("diagnosis/{id:int}/sign")]
        public async Task<IActionResult> Sign(
            [FromServices] IMediator mediator,
            int id,
            [FromBody] SignDiagnosisRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.Id = id;
            var ok = await mediator.Send(command, cancellationToken);
            return ok ? CustomResponse() : CustomResponse(null, HttpStatusCode.BadRequest);
        }

        [HttpPost("diagnosis/{id:int}/reject")]
        public async Task<IActionResult> Reject(
            [FromServices] IMediator mediator,
            int id,
            [FromBody] RejectDiagnosisRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.Id = id;
            var ok = await mediator.Send(command, cancellationToken);
            return ok ? CustomResponse() : CustomResponse(null, HttpStatusCode.BadRequest);
        }

        [HttpGet("clients")]
        public async Task<IActionResult> GetClients(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetAgronomistClientsRequest(), cancellationToken);
            return CustomResponse(result);
        }

        [HttpPost("clients/invite")]
        public async Task<IActionResult> InviteClient(
            [FromServices] IMediator mediator,
            [FromBody] InviteClientRequest command,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await mediator.Send(command, cancellationToken);
            if (result is null) return CustomResponse();

            return CustomResponse(result, HttpStatusCode.Created);
        }

        [HttpDelete("clients/{linkId:int}")]
        public async Task<IActionResult> RevokeClient(
            [FromServices] IMediator mediator,
            int linkId,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new RevokeClientRequest { LinkId = linkId }, cancellationToken);
            return ok ? CustomResponse(null, HttpStatusCode.NoContent) : CustomResponse(null, HttpStatusCode.NotFound);
        }
    }
}
