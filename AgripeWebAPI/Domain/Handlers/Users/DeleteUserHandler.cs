using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class DeleteUserHandler : IRequestHandler<DeleteUserRequest, DeleteUserResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;
        private readonly ILogger<DeleteUserHandler> _logger;

        public DeleteUserHandler(agpDBContext dbContext, INotifier notifier, ILogger<DeleteUserHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DeleteUserResponse> Handle(DeleteUserRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.Find(u => u.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Attempt to delete non-existent user: {UserId}", request.Id);
                _notifier.Handle(new Notification("Usuário não encontrado."));
                return new DeleteUserResponse { Success = false };
            }

            // Authorization check: Only allow users to delete their own account
            if (request.CurrentUserId > 0 && user.Id != request.CurrentUserId)
            {
                _logger.LogWarning("Unauthorized delete attempt: User {CurrentUserId} tried to delete user {TargetUserId}", request.CurrentUserId, user.Id);
                _notifier.Handle(new Notification("Você não tem permissão para deletar este usuário."));
                return new DeleteUserResponse { Success = false };
            }

            try
            {
                await _dbContext.Users.DeleteOneAsync(x => x.Id == user.Id, cancellationToken);

                _logger.LogInformation("User deleted successfully: {UserId}, {Email}", user.Id, user.Email);

                return new DeleteUserResponse { Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", request.Id);
                _notifier.Handle(new Notification("Erro ao deletar usuário. Tente novamente."));
                return new DeleteUserResponse { Success = false };
            }
        }
    }
}
