using StarkAgroAPI.Domain.Commands.Requests.Anomalies;
using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace StarkAgroAPI.Controllers
{
    [Authorize]
    [Route("v1/pivot")]
    public class PivotAnalyticsController : MainController
    {
        public PivotAnalyticsController(INotifier notificador) : base(notificador)
        {
        }

        [Route("getIrrigationTrend")]
        [HttpGet]
        public async Task<IActionResult> GetIrrigationTrend(
            [FromServices] IMediator mediator,
            [FromQuery] GetIrrigationTrendRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            var result = await mediator.Send(command, cancellationToken);

            return CustomResponse(result!);
        }

        [Route("{pivotId:int}/anomalies")]
        [HttpGet]
        public async Task<IActionResult> GetAnomalies(
            [FromServices] IMediator mediator,
            int pivotId,
            [FromQuery] bool? acknowledged = null,
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageIndex = 0,
            CancellationToken cancellationToken = default
        )
        {
            var command = new GetPivotAnomaliesRequest
            {
                PivotId = pivotId,
                UserId = GetCurrentUserId(),
                AcknowledgedOnly = acknowledged,
                PageSize = pageSize,
                PageIndex = pageIndex
            };
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result);
        }

        [Route("{pivotId:int}/ai-insights")]
        [HttpPost]
        public async Task<IActionResult> GetAIInsights(
            [FromServices] IMediator mediator,
            int pivotId,
            CancellationToken cancellationToken
        )
        {
            var command = new GetPivotAIInsightsRequest
            {
                PivotId = pivotId,
                UserId = GetCurrentUserId()
            };
            var result = await mediator.Send(command, cancellationToken);
            if (result is null)
                return CustomResponse(result, HttpStatusCode.ServiceUnavailable);
            return CustomResponse(result);
        }

        [Route("forecast")]
        [HttpGet]
        public async Task<IActionResult> GetForecast(
            [FromServices] IMediator mediator,
            [FromQuery] GetPivotForecastRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            var result = await mediator.Send(command, cancellationToken);

            return CustomResponse(result!);
        }

        [Route("{pivotId:int}/moisture-prediction")]
        [HttpGet]
        public async Task<IActionResult> GetMoisturePrediction(
            [FromServices] IMediator mediator,
            int pivotId,
            CancellationToken cancellationToken
        )
        {
            var command = new GetMoisturePredictionRequest
            {
                PivotId = pivotId,
                UserId = GetCurrentUserId()
            };
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }
    }
}
