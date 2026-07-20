using StarkAgroAPI.Domain.Commands.Requests.WaterSources;
using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.WaterSources
{
    public class EditWaterSourceHandler : IRequestHandler<EditWaterSourceRequest, WaterSourceResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public EditWaterSourceHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<WaterSourceResponse?> Handle(EditWaterSourceRequest request, CancellationToken cancellationToken)
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

            var existing = await _dbContext.WaterSources
                .Find(w => w.Id == request.Id && w.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                _notifier.Handle(new Notification("WaterSource not found."));
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

            var update = Builders<Models.Entities.WaterSource>.Update
                .Set(w => w.Name, request.Name)
                .Set(w => w.PivotIds, request.PivotIds)
                .Set(w => w.MaxFlowLitersPerHour, request.MaxFlowLitersPerHour);

            await _dbContext.WaterSources.UpdateOneAsync(
                w => w.Id == request.Id && w.UserId == userId,
                update,
                cancellationToken: cancellationToken);

            existing.Name = request.Name;
            existing.PivotIds = request.PivotIds;
            existing.MaxFlowLitersPerHour = request.MaxFlowLitersPerHour;

            return CreateWaterSourceHandler.ToResponse(existing);
        }
    }
}
