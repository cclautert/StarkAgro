using StarkAgroAPI.Configuration;
using StarkAgroAPI.Domain.Commands.Requests.Irrigation;
using StarkAgroAPI.Domain.Commands.Responses.Irrigation;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Irrigation
{
    public class ScheduleProposalHandler : IRequestHandler<ScheduleProposalRequest, ScheduleProposalResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IWeatherForecastService _forecastService;
        private readonly INotifier _notifier;
        private readonly WeatherForecastSettings _settings;

        public ScheduleProposalHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IWeatherForecastService forecastService,
            INotifier notifier,
            IOptions<WeatherForecastSettings> settings)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<ScheduleProposalResponse?> Handle(ScheduleProposalRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            if (request.TypicalDurationMinutes <= 0)
            {
                _notifier.Handle(new Notification("TypicalDurationMinutes must be greater than zero."));
                return null;
            }

            var waterSource = await _dbContext.WaterSources
                .Find(w => w.Id == request.WaterSourceId && w.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (waterSource is null)
            {
                _notifier.Handle(new Notification("WaterSource not found."));
                return null;
            }

            if (waterSource.PivotIds.Count == 0)
            {
                _notifier.Handle(new Notification("WaterSource has no associated pivots."));
                return null;
            }

            var pivots = await _dbContext.Pivots
                .Find(p => waterSource.PivotIds.Contains(p.Id) && p.UserId == userId)
                .ToListAsync(cancellationToken);

            var rainThreshold = request.RainThresholdMm ?? _settings.RainThresholdMm;
            var horizonStart = RoundUpToNextHour(DateTime.UtcNow);
            var horizonEnd = horizonStart.AddHours(48);

            var pivotInputs = await BuildPivotInputsAsync(pivots, waterSource.PivotIds, cancellationToken);

            var windows = GreedyIrrigationScheduler.Build(
                pivotInputs,
                waterSource.MaxFlowLitersPerHour,
                request.TypicalDurationMinutes,
                rainThreshold,
                horizonStart,
                horizonEnd);

            var proposalWindows = windows.Select(w => new IrrigationWindow
            {
                PivotId = w.PivotId,
                StartUtc = w.StartUtc,
                EndUtc = w.EndUtc,
                EstimatedMm = w.EstimatedMm
            }).ToList();

            var proposal = new IrrigationProposal
            {
                Id = await _dbContext.GetNextIdAsync(nameof(IrrigationProposal), cancellationToken),
                UserId = userId,
                WaterSourceId = waterSource.Id,
                Status = ProposalStatus.Pending,
                Windows = proposalWindows,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.IrrigationProposals.InsertOneAsync(proposal, cancellationToken: cancellationToken);

            return new ScheduleProposalResponse
            {
                ProposalId = proposal.Id,
                Windows = proposalWindows.Select(w => new IrrigationWindowDto
                {
                    PivotId = w.PivotId,
                    StartUtc = w.StartUtc,
                    EndUtc = w.EndUtc,
                    EstimatedMm = w.EstimatedMm
                }).ToList()
            };
        }

        private async Task<List<GreedyIrrigationScheduler.PivotInput>> BuildPivotInputsAsync(
            List<Pivot> pivots,
            List<int> orderedPivotIds,
            CancellationToken cancellationToken)
        {
            var pivotMap = pivots.ToDictionary(p => p.Id);
            var inputs = new List<GreedyIrrigationScheduler.PivotInput>();

            foreach (var pivotId in orderedPivotIds)
            {
                if (!pivotMap.TryGetValue(pivotId, out var pivot))
                    continue;

                WeatherForecast? forecast = null;
                if (pivot.Latitude.HasValue && pivot.Longitude.HasValue)
                {
                    try
                    {
                        forecast = await _forecastService.GetForecastAsync(
                            pivot.Latitude.Value,
                            pivot.Longitude.Value,
                            days: 2,
                            cancellationToken);
                    }
                    catch
                    {
                        // forecast unavailable — allow irrigation
                    }
                }

                inputs.Add(new GreedyIrrigationScheduler.PivotInput(
                    pivot.Id,
                    pivot.Latitude,
                    pivot.Longitude,
                    forecast));
            }

            return inputs;
        }

        private static DateTime RoundUpToNextHour(DateTime utcNow)
        {
            if (utcNow.Minute == 0 && utcNow.Second == 0 && utcNow.Millisecond == 0)
                return utcNow;
            return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc)
                .AddHours(1);
        }
    }
}
