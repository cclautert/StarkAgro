using AgripeWebAPI.Domain.Commands.Requests.Diagnosis;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.Diagnosis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/diagnosis")]
    public class PlantDiagnosisController : MainController
    {
        public PlantDiagnosisController(INotifier notificador) : base(notificador)
        {
        }

        /// <summary>
        /// Recebe a foto do produtor e devolve 202: a análise é assíncrona, feita pelo worker.
        /// Segurar a request esperando a IA custaria 10–30s de espera com a conexão aberta.
        /// </summary>
        [HttpPost]
        [EnableRateLimiting("diagnosis-upload")]
        [RequestSizeLimit(ImageContentValidator.MaxSizeBytes)]
        public async Task<IActionResult> Create(
            [FromServices] IMediator mediator,
            [FromForm] IFormFile? image,
            [FromForm] int? pivotId,
            [FromForm] string? cropName,
            [FromForm] string? notes,
            [FromForm] double? latitude,
            [FromForm] double? longitude,
            CancellationToken cancellationToken)
        {
            if (image is null || image.Length == 0)
            {
                NotifyError("A foto é obrigatória.");
                return CustomResponse();
            }

            if (image.Length > ImageContentValidator.MaxSizeBytes)
            {
                NotifyError("A foto excede o tamanho máximo de 12 MB.");
                return CustomResponse();
            }

            using var stream = new MemoryStream();
            await image.CopyToAsync(stream, cancellationToken);

            var command = new CreatePlantDiagnosisRequest
            {
                ImageBytes = stream.ToArray(),
                FileName = image.FileName,
                ContentType = image.ContentType,
                PivotId = pivotId,
                CropName = cropName,
                Notes = notes,
                Latitude = latitude,
                Longitude = longitude
            };

            var result = await mediator.Send(command, cancellationToken);
            if (result is null) return CustomResponse();

            return CustomResponse(result, HttpStatusCode.Accepted);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromServices] IMediator mediator,
            [FromQuery] string? status,
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageIndex = 0,
            CancellationToken cancellationToken = default)
        {
            var result = await mediator.Send(
                new GetPlantDiagnosisListRequest { Status = status, PageSize = pageSize, PageIndex = pageIndex },
                cancellationToken);

            return CustomResponse(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetPlantDiagnosisByIdRequest { Id = id }, cancellationToken);
            if (result is null) return CustomResponse(null, HttpStatusCode.NotFound);

            return CustomResponse(result);
        }

        /// <summary>Polling barato enquanto o laudo está sendo processado.</summary>
        [HttpGet("{id:int}/status")]
        public async Task<IActionResult> GetStatus(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetPlantDiagnosisStatusRequest { Id = id }, cancellationToken);
            if (result is null) return CustomResponse(null, HttpStatusCode.NotFound);

            return CustomResponse(result);
        }

        [HttpGet("{id:int}/image")]
        public async Task<IActionResult> GetImage(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetPlantDiagnosisImageRequest { Id = id }, cancellationToken);
            if (result is null) return NotFound();

            Response.Headers.CacheControl = "private, max-age=3600";
            return File(result.Content, result.ContentType);
        }

        /// <summary>PDF do laudo — o documento que o produtor guarda e imprime.</summary>
        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetDiagnosisPdfRequest { Id = id }, cancellationToken);
            if (result is null) return NotFound();

            return File(result.Content, "application/pdf", result.FileName);
        }

        /// <summary>Reenfileira um laudo que falhou, sem exigir que o produtor reenvie a foto.</summary>
        [HttpPost("{id:int}/reprocess")]
        public async Task<IActionResult> Reprocess(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var ok = await mediator.Send(new ReprocessDiagnosisRequest { Id = id }, cancellationToken);
            return ok ? CustomResponse(null, HttpStatusCode.Accepted) : CustomResponse();
        }

        /// <summary>Histórico do talhão: "a mancha piorou desde a última vez?".</summary>
        [HttpGet("history/{pivotId:int}")]
        public async Task<IActionResult> GetHistory(
            [FromServices] IMediator mediator,
            int pivotId,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetDiagnosisHistoryRequest { PivotId = pivotId }, cancellationToken);
            return CustomResponse(result);
        }

        /// <summary>Quantos laudos o produtor ainda tem neste mês.</summary>
        [HttpGet("quota")]
        public async Task<IActionResult> GetQuota(
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetDiagnosisQuotaRequest(), cancellationToken);
            return CustomResponse(result);
        }

        [HttpGet("{id:int}/audit")]
        public async Task<IActionResult> GetAudit(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetDiagnosisAuditRequest { Id = id }, cancellationToken);
            if (result is null) return CustomResponse(null, HttpStatusCode.NotFound);

            return CustomResponse(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(
            [FromServices] IMediator mediator,
            int id,
            CancellationToken cancellationToken)
        {
            var deleted = await mediator.Send(new DeletePlantDiagnosisRequest { Id = id }, cancellationToken);
            if (!deleted) return CustomResponse(null, HttpStatusCode.NotFound);

            return CustomResponse(null, HttpStatusCode.NoContent);
        }
    }
}
