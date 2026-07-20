using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Pivots
{
    public class EditPivotHandler : IRequestHandler<EditPivotRequest, EditPivotResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public EditPivotHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<EditPivotResponse?> Handle(EditPivotRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to edit a pivot.");

            if (request.Id is null || request.Id <= 0)
            {
                _notifier.Handle(new Notification("Pivot id is required."));
                return null;
            }

            if (!PivotLocationValidator.Validate(request.Latitude, request.Longitude, request.Altitude, _notifier))
            {
                return null;
            }

            var pivot = await _dbContext.Pivots
                .Find(p => p.Id == request.Id && p.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pivot is null)
            {
                _notifier.Handle(new Notification("Pivot not found."));
                return null;
            }

            var coordinatesChanged =
                pivot.Latitude != request.Latitude ||
                pivot.Longitude != request.Longitude ||
                pivot.Altitude != request.Altitude;

            pivot.Name = request.Name;
            pivot.Latitude = request.Latitude;
            pivot.Longitude = request.Longitude;
            pivot.Altitude = request.Altitude;
            pivot.LocationAddress = request.LocationAddress;
            if (coordinatesChanged)
            {
                pivot.LocationUpdatedAt = request.Latitude.HasValue && request.Longitude.HasValue
                    ? DateTime.UtcNow
                    : null;
            }

            await _dbContext.Pivots.ReplaceOneAsync(
                x => x.Id == pivot.Id && x.UserId == userId,
                pivot,
                cancellationToken: cancellationToken);

            return new EditPivotResponse { Id = pivot.Id };
        }
    }
}
