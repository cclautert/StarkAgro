using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class GetUserHandler : IRequestHandler<GetUserRequest, GetUserResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;
        private readonly ILogger<GetUserHandler> _logger;

        public GetUserHandler(agpDBContext dbContext, INotifier notifier, ILogger<GetUserHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<GetUserResponse?> Handle(GetUserRequest request, CancellationToken cancellationToken)
        {
            // Authorization check: Only allow users to access their own data
            if (request.CurrentUserId > 0 && request.Id != request.CurrentUserId)
            {
                _logger.LogWarning("Unauthorized access attempt: User {CurrentUserId} tried to access user {RequestedUserId}", request.CurrentUserId, request.Id);
                _notifier.Handle(new Notification("Você não tem permissão para acessar este usuário."));
                return null;
            }

            var user = await _dbContext.Users
                .Find(x => x.Id == request.Id)
                .Project(x => new GetUserResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Email = x.Email,
                    LimiteInferior = x.LimiteInferior,
                    LimiteSuperior = x.LimiteSuperior,
                    RainThresholdMm = x.RainThresholdMm,
                    UplinkIntervalSeconds = x.UplinkIntervalSeconds
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", request.Id);
                _notifier.Handle(new Notification("Usuário não encontrado."));
                return null;
            }

            return user;
        }
    }
}
