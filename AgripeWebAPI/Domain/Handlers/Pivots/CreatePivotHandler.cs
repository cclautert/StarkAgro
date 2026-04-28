using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class CreatePivotHandler : IRequestHandler<CreatePivotRequest, CreatePivotResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public CreatePivotHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<CreatePivotResponse?> Handle(CreatePivotRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to create a pivot.");

            if (!PivotLocationValidator.Validate(request.Latitude, request.Longitude, request.Altitude, _notifier))
            {
                return null;
            }

            var hasCoordinates = request.Latitude.HasValue && request.Longitude.HasValue;

            var pivot = new Pivot
            {
                Id = await _dbContext.GetNextIdAsync(nameof(Pivot), cancellationToken),
                Name = request.Name,
                UserId = userId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Altitude = request.Altitude,
                LocationAddress = request.LocationAddress,
                LocationUpdatedAt = hasCoordinates ? DateTime.UtcNow : null
            };

            await _dbContext.Pivots.InsertOneAsync(pivot, cancellationToken: cancellationToken);

            return new CreatePivotResponse
            {
                Id = pivot.Id
            };
        }
    }
}
