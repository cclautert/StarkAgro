using StarkAgroAPI.Domain.Commands.Requests.WaterSources;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.WaterSources
{
    public class DeleteWaterSourceHandler : IRequestHandler<DeleteWaterSourceRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public DeleteWaterSourceHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<bool> Handle(DeleteWaterSourceRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var filter = Builders<WaterSource>.Filter.Where(w => w.Id == request.Id && w.UserId == userId);
            var result = await _dbContext.WaterSources.DeleteOneAsync(filter, cancellationToken);

            if (result.DeletedCount == 0)
            {
                _notifier.Handle(new Notification("WaterSource not found."));
                return false;
            }

            return true;
        }
    }
}
