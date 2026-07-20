using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Commands.Responses.Users;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Users
{
    public class GetToken : IRequestHandler<UserTokenRequest, UserTokenResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<GetToken> _logger;
        private readonly ILoginAttemptService _loginAttemptService;

        public GetToken(agpDBContext dbContext, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService, ILogger<GetToken> logger, ILoginAttemptService loginAttemptService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loginAttemptService = loginAttemptService ?? throw new ArgumentNullException(nameof(loginAttemptService));
        }

        public async Task<UserTokenResponse> Handle(UserTokenRequest request, CancellationToken cancellationToken)
        {
            if (_loginAttemptService.IsLockedOut(request.Email))
            {
                _logger.LogWarning("Login blocked - too many attempts for email: {Email}", request.Email);
                return new UserTokenResponse { ErrorCode = LoginErrorCode.TooManyAttempts };
            }

            // E-mail não diferencia caixa: quem se cadastrou como "Fulano@Fazenda.com"
            // precisa conseguir logar digitando "fulano@fazenda.com".
            User? user = await _dbContext.Users
                .Find(EmailNormalizer.ByEmail(request.Email))
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                _loginAttemptService.RecordFailure(request.Email);
                return new UserTokenResponse { ErrorCode = LoginErrorCode.InvalidCredentials };
            }

            if (!user.Active)
            {
                _logger.LogWarning("Login attempt for inactive user: {UserId}, {Email}", user.Id, request.Email);
                return new UserTokenResponse { ErrorCode = LoginErrorCode.AccountInactive };
            }

            if (!_passwordHasher.VerifyPassword(request.Password, user.Password))
            {
                _logger.LogWarning("Failed login attempt - invalid password for user: {UserId}, {Email}", user.Id, request.Email);
                _loginAttemptService.RecordFailure(request.Email);
                return new UserTokenResponse { ErrorCode = LoginErrorCode.InvalidCredentials };
            }

            // If password was plain text (legacy), hash it now for future logins
            if (!IsBcryptHash(user.Password))
            {
                _logger.LogInformation("Migrating plain text password to BCrypt for user: {UserId}, {Email}", user.Id, request.Email);
                user.Password = _passwordHasher.HashPassword(request.Password);
                await _dbContext.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken);
            }

            _loginAttemptService.ResetFailures(request.Email);
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
