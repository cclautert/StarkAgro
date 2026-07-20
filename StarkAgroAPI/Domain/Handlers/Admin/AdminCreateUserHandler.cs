using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Services;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    public class AdminCreateUserHandler : IRequestHandler<AdminCreateUserRequest, AdminUserResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly INotifier _notifier;
        private readonly ILogger<AdminCreateUserHandler> _logger;

        public AdminCreateUserHandler(agpDBContext dbContext, IPasswordHasher passwordHasher, INotifier notifier, ILogger<AdminCreateUserHandler> logger)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _notifier = notifier;
            _logger = logger;
        }

        public async Task<AdminUserResponse> Handle(AdminCreateUserRequest request, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.Users.Find(EmailNormalizer.ByEmail(request.Email)).FirstOrDefaultAsync(cancellationToken);
            if (existing != null)
            {
                _notifier.Handle(new Notification("Email já cadastrado."));
                return null!;
            }

            try
            {
                var user = new User
                {
                    Id = await _dbContext.GetNextIdAsync(nameof(User), cancellationToken),
                    Name = request.Name,
                    Email = EmailNormalizer.Normalize(request.Email),
                    Password = _passwordHasher.HashPassword(request.Password),
                    Active = request.Active,
                    IsAdmin = request.IsAdmin
                };

                await _dbContext.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
                _logger.LogInformation("Admin criou usuário: {Email}", request.Email);

                return new AdminUserResponse { Id = user.Id, Name = user.Name, Email = user.Email, Active = user.Active, IsAdmin = user.IsAdmin };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar usuário via admin: {Email}", request.Email);
                _notifier.Handle(new Notification("Erro ao criar usuário."));
                return null!;
            }
        }
    }
}
