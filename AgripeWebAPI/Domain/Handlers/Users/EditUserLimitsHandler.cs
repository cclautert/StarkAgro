using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class EditUserLimitsHandler : IRequestHandler<EditUserLimitsRequest, EditUserResponse>
    {
        private readonly agpDBContext _dbContext;

        public EditUserLimitsHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<EditUserResponse> Handle(EditUserLimitsRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Find(u => u.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
                throw new KeyNotFoundException("User not found");

            user.LimiteInferior = request.LimiteInferior;
            user.LimiteSuperior = request.LimiteSuperior;
            user.RainThresholdMm = request.RainThresholdMm;
            if (request.UplinkIntervalSeconds.HasValue)
                user.UplinkIntervalSeconds = request.UplinkIntervalSeconds.Value;

            await _dbContext.Users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: cancellationToken);

            return new EditUserResponse { Id = user.Id, Name = user.Name, Email = user.Email };
        }
    }
}
