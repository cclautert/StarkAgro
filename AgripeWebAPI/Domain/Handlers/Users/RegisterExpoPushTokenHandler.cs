using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class RegisterExpoPushTokenHandler : IRequestHandler<RegisterExpoPushTokenRequest, Unit>
    {
        private readonly agpDBContext _dbContext;

        public RegisterExpoPushTokenHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<Unit> Handle(RegisterExpoPushTokenRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Find(u => u.Id == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
                throw new KeyNotFoundException($"User {request.UserId} not found");

            user.ExpoPushToken = request.Token.Trim();
            await _dbContext.Users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: cancellationToken);

            return Unit.Value;
        }
    }
}
