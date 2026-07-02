using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class MarkAlertsReadHandler : IRequestHandler<MarkAlertsReadRequest, Unit>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public MarkAlertsReadHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<Unit> Handle(MarkAlertsReadRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to mark alerts as read.");

            var update = Builders<User>.Update.Set(u => u.AlertsReadAt, DateTime.UtcNow);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == userId, update, cancellationToken: cancellationToken);

            return Unit.Value;
        }
    }
}
