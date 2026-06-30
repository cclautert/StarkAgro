using AgripeWebAPI.Domain.Commands.Requests.Admin;
using AgripeWebAPI.Domain.Commands.Responses.Admin;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Admin
{
    public class AdminEditUserHandler : IRequestHandler<AdminEditUserRequest, AdminUserResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly INotifier _notifier;

        public AdminEditUserHandler(agpDBContext dbContext, IPasswordHasher passwordHasher, INotifier notifier)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _notifier = notifier;
        }

        public async Task<AdminUserResponse> Handle(AdminEditUserRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.Find(u => u.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                _notifier.Handle(new Notification("Usuário não encontrado."));
                return null!;
            }

            var emailConflict = await _dbContext.Users
                .Find(u => u.Email == request.Email && u.Id != request.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (emailConflict != null)
            {
                _notifier.Handle(new Notification("Email já em uso por outro usuário."));
                return null!;
            }

            user.Name = request.Name;
            user.Email = request.Email;
            user.Active = request.Active;
            user.IsAdmin = request.IsAdmin;

            if (!string.IsNullOrWhiteSpace(request.Password))
                user.Password = _passwordHasher.HashPassword(request.Password);

            await _dbContext.Users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: cancellationToken);

            return new AdminUserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Active = user.Active,
                IsAdmin = user.IsAdmin,
                LimiteInferior = user.LimiteInferior,
                LimiteSuperior = user.LimiteSuperior,
                RainThresholdMm = user.RainThresholdMm,
                GeminiApiKey = user.GeminiApiKey,
                UplinkIntervalSeconds = user.UplinkIntervalSeconds
            };
        }
    }
}
