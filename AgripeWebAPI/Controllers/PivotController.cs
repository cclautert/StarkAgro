using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AgripeWebAPI.Controllers
{
    [Authorize]
    [Route("v1/pivot")]
    public class PivotController : MainController
    {
        public PivotController(INotifier notificador) : base(notificador)
        {
        }

        [Route("getById")]
        [HttpGet]
        public async Task<ActionResult<GetPivotResponse>> GetById(
            [FromServices] IMediator mediator,
            [FromQuery] GetPivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("getAll")]
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromServices] IMediator mediator,
            [FromQuery] GetListPivotByUserIdRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            List<GetPivotResponse> lstPivots = await mediator.Send(command, cancellationToken);

            return CustomResponse(lstPivots);
        }

        [Route("add")]
        [HttpPost]
        public async Task<IActionResult> Add(
            [FromServices] IMediator mediator,
            [FromBody] CreatePivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            command.UserId = GetCurrentUserId();
            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }

        [Route("update")]
        [HttpPut]
        public async Task<IActionResult> Update(
            [FromServices] IMediator mediator,
            [FromBody] EditPivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            var result = await mediator.Send(command, cancellationToken);
            return CustomResponse(result!);
        }

        [Route("updateLimits")]
        [HttpPut]
        public async Task<ActionResult<EditPivotResponse>> UpdateLimits(
            [FromServices] IMediator mediator,
            [FromBody] EditPivotLimitsRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
        }

        [Route("delete")]
        [HttpDelete]
        public async Task<ActionResult<DeletePivotResponse>> Delete(
            [FromServices] IMediator mediator,
            [FromQuery] DeletePivotRequest command,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid) return CustomResponse(ModelState);

            return await mediator.Send(command, cancellationToken);
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
