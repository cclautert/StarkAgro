using StarkAgroAPI.Domain.Commands.Requests.WaterSources;
using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.WaterSources
{
    public class GetWaterSourceHandler : IRequestHandler<GetWaterSourceRequest, WaterSourceResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public GetWaterSourceHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<WaterSourceResponse?> Handle(GetWaterSourceRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var entity = await _dbContext.WaterSources
                .Find(w => w.Id == request.Id && w.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                _notifier.Handle(new Notification("WaterSource not found."));
                return null;
            }

            return CreateWaterSourceHandler.ToResponse(entity);
        }
    }
}
