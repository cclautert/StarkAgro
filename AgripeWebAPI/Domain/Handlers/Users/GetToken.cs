using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class GetToken : IRequestHandler<UserTokenRequest, UserTokenResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<GetToken> _logger;

        public GetToken(agpDBContext dbContext, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService, ILogger<GetToken> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserTokenResponse> Handle(UserTokenRequest request, CancellationToken cancellationToken)
        {
            User? user = await _dbContext.Users
                .Where(x => x.Email == request.Email)
                .Select(x => new User
                {
                    Id = x.Id,
                    Name = x.Name,
                    Email = x.Email,
                    Password = x.Password,
                    Active = x.Active
                }).SingleOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                return null!;
            }

            if (!user.Active)
            {
                _logger.LogWarning("Login attempt for inactive user: {UserId}, {Email}", user.Id, request.Email);
                return null!;
            }

            if (!_passwordHasher.VerifyPassword(request.Password, user.Password))
            {
                _logger.LogWarning("Failed login attempt - invalid password for user: {UserId}, {Email}", user.Id, request.Email);
                return null!;
            }

            // If password was plain text (legacy), hash it now for future logins
            if (!IsBcryptHash(user.Password))
            {
                _logger.LogInformation("Migrating plain text password to BCrypt for user: {UserId}, {Email}", user.Id, request.Email);
                user.Password = _passwordHasher.HashPassword(request.Password);
                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Successful login for user: {UserId}, {Email}", user.Id, request.Email);

            return new UserTokenResponse { Token = await _jwtTokenService.GenerateTokenAsync(user, cancellationToken) };
        }

        /// <summary>
        /// Checks if a string is a valid BCrypt hash format
        /// </summary>
        private bool IsBcryptHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            // BCrypt hashes start with $2a$, $2b$, $2x$, or $2y$ followed by $ and cost parameter
            return hash.StartsWith("$2a$") || 
                   hash.StartsWith("$2b$") || 
                   hash.StartsWith("$2x$") || 
                   hash.StartsWith("$2y$");
        }
    }
}
