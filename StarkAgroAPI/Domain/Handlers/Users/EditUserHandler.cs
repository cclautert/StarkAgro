using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Commands.Responses.Users;
using StarkAgroAPI.Models;
using StarkAgroAPI.Services;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Validators;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Handlers.Users
{
    public class EditUserHandler : IRequestHandler<EditUserRequest, EditUserResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly INotifier _notifier;
        private readonly ILogger<EditUserHandler> _logger;
        private static readonly PasswordStrengthAttribute PasswordValidator = new();

        public EditUserHandler(agpDBContext dbContext, IPasswordHasher passwordHasher, INotifier notifier, ILogger<EditUserHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<EditUserResponse> Handle(EditUserRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Find(EmailNormalizer.ByEmail(request.Email))
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Attempt to edit non-existent user with email: {Email}", request.Email);
                _notifier.Handle(new Notification("Email não encontrado."));
                return null!;
            }

            // Authorization check: Only allow users to edit their own data
            if (request.CurrentUserId > 0 && user.Id != request.CurrentUserId)
            {
                _logger.LogWarning("Unauthorized edit attempt: User {CurrentUserId} tried to edit user {TargetUserId}, {Email}", request.CurrentUserId, user.Id, request.Email);
                _notifier.Handle(new Notification("Você não tem permissão para editar este usuário."));
                return null!;
            }

            try
            {
                var passwordChanged = false;
                user.Name = request.Name;
                user.Email = EmailNormalizer.Normalize(request.Email);
                
                // Only hash password if a new one is provided and validate it
                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    if (!PasswordValidator.IsValid(request.Password))
                    {
                        _logger.LogWarning("Invalid password strength for user: {UserId}, {Email}", user.Id, request.Email);
                        _notifier.Handle(new Notification("A senha não atende aos requisitos de segurança."));
                        return null!;
                    }

                    user.Password = _passwordHasher.HashPassword(request.Password);
                    passwordChanged = true;
                    _logger.LogInformation("Password change initiated for user: {UserId}, {Email}", user.Id, request.Email);
                }

                await _dbContext.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken);

                if (passwordChanged)
                {
                    _logger.LogInformation("Password changed successfully for user: {UserId}, {Email}", user.Id, request.Email);
                }

                _logger.LogInformation("User updated successfully: {UserId}, {Email}", user.Id, request.Email);

                return new EditUserResponse
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with email: {Email}", request.Email);
                _notifier.Handle(new Notification("Erro ao atualizar usuário. Tente novamente."));
                return null!;
            }
        }
    }
}
