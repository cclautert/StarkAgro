using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class CreateUserHandler : IRequestHandler<CreateUserRequest, CreateUserResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly INotifier _notifier;
        private readonly ILogger<CreateUserHandler> _logger;

        public CreateUserHandler(agpDBContext dbContext, IPasswordHasher passwordHasher, INotifier notifier, ILogger<CreateUserHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CreateUserResponse> Handle(CreateUserRequest request, CancellationToken cancellationToken)
        {
            var user = _dbContext.Users.Where(x => x.Email == request.Email).FirstOrDefault();

            if (user != null)
            {
                _logger.LogWarning("Attempt to create user with existing email: {Email}", request.Email);
                _notifier.Handle(new Notification("Email já cadastrado."));
                return null!;
            }

            try
            {
                var hashedPassword = _passwordHasher.HashPassword(request.Password);
                var userUpdated = _dbContext.Users.Add(new User { Name = request.Name, Email = request.Email, Password = hashedPassword, Active = true });
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("User created successfully: {UserId}, {Email}", userUpdated.Entity.Id, request.Email);

                return new CreateUserResponse { Id = userUpdated.Entity.Id, Name = userUpdated.Entity.Name, Email = userUpdated.Entity.Email };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with email: {Email}", request.Email);
                _notifier.Handle(new Notification("Erro ao criar usuário. Tente novamente."));
                return null!;
            }
        }
    }
}
