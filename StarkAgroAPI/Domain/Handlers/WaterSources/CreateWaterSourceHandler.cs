using StarkAgroAPI.Domain.Commands.Requests.WaterSources;
using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.WaterSources
{
    public class CreateWaterSourceHandler : IRequestHandler<CreateWaterSourceRequest, WaterSourceResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public CreateWaterSourceHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<WaterSourceResponse?> Handle(CreateWaterSourceRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                _notifier.Handle(new Notification("Name is required."));
                return null;
            }

            if (request.MaxFlowLitersPerHour <= 0)
            {
                _notifier.Handle(new Notification("MaxFlowLitersPerHour must be greater than zero."));
                return null;
            }

            if (request.PivotIds.Count > 0)
            {
                var validPivots = await _dbContext.Pivots
                    .Find(p => request.PivotIds.Contains(p.Id) && p.UserId == userId)
                    .ToListAsync(cancellationToken);

                if (validPivots.Count != request.PivotIds.Distinct().Count())
                {
                    _notifier.Handle(new Notification("One or more pivotIds are invalid or not owned by the current user."));
                    return null;
                }
            }

            var entity = new WaterSource
            {
                Id = await _dbContext.GetNextIdAsync(nameof(WaterSource), cancellationToken),
                UserId = userId,
                Name = request.Name,
                PivotIds = request.PivotIds,
                MaxFlowLitersPerHour = request.MaxFlowLitersPerHour
            };

            await _dbContext.WaterSources.InsertOneAsync(entity, cancellationToken: cancellationToken);

            return ToResponse(entity);
        }

        internal static WaterSourceResponse ToResponse(WaterSource ws) => new()
        {
            Id = ws.Id,
            Name = ws.Name,
            PivotIds = ws.PivotIds,
            MaxFlowLitersPerHour = ws.MaxFlowLitersPerHour
        };
    }
}
