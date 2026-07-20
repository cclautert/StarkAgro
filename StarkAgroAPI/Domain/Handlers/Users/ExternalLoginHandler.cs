using StarkAgroAPI.Configuration;
using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Commands.Responses.Users;
using StarkAgroAPI.Models;
using StarkAgroAPI.Services;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StarkAgroAPI.Domain.Handlers.Users
{
    public class ExternalLoginHandler : IRequestHandler<ExternalLoginRequest, UserTokenResponse?>
    {
        private const string GoogleProvider = "Google";
        private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string GoogleUserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";

        private readonly agpDBContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly OAuthSettings _oauthSettings;
        private readonly ILogger<ExternalLoginHandler> _logger;

        public ExternalLoginHandler(
            agpDBContext dbContext,
            IHttpClientFactory httpClientFactory,
            IJwtTokenService jwtTokenService,
            IPasswordHasher passwordHasher,
            IOptions<OAuthSettings> oauthSettings,
            ILogger<ExternalLoginHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _oauthSettings = oauthSettings?.Value ?? throw new ArgumentNullException(nameof(oauthSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserTokenResponse?> Handle(ExternalLoginRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.Code))
            {
                _logger.LogWarning("External login missing provider or code.");
                return null;
            }

            if (!string.Equals(request.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unsupported OAuth provider: {Provider}", request.Provider);
                return null;
            }

            var google = _oauthSettings.Google;
            if (string.IsNullOrWhiteSpace(google?.ClientId) || string.IsNullOrWhiteSpace(google?.ClientSecret))
            {
                _logger.LogWarning("Google OAuth is not configured.");
                return null;
            }

            var redirectUri = request.RedirectUri?.Trim() ?? string.Empty;
            var allowedUris = google.AllowedRedirectUris?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(u => u.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            if (allowedUris.Count > 0 && !allowedUris.Contains(redirectUri))
            {
                _logger.LogWarning("Redirect URI not allowed: {RedirectUri}", redirectUri);
                return null;
            }

            var httpClient = _httpClientFactory.CreateClient();

            // Exchange authorization code for tokens
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = request.Code,
                ["client_id"] = google.ClientId,
                ["client_secret"] = google.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

            HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await httpClient.PostAsync(GoogleTokenEndpoint, tokenRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to exchange code with Google.");
                return null;
            }

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var body = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Google token exchange failed: {StatusCode}, {Body}", tokenResponse.StatusCode, body);
                return null;
            }

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
            if (tokenData?.AccessToken == null)
            {
                _logger.LogWarning("Google token response missing access_token.");
                return null;
            }

            // Get user info from Google
            var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, GoogleUserInfoEndpoint);
            userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);

            HttpResponseMessage userInfoResponse;
            try
            {
                userInfoResponse = await httpClient.SendAsync(userInfoRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user info from Google.");
                return null;
            }

            if (!userInfoResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google userinfo failed: {StatusCode}", userInfoResponse.StatusCode);
                return null;
            }

            var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>(cancellationToken);
            if (userInfo?.Email == null)
            {
                _logger.LogWarning("Google userinfo missing email.");
                return null;
            }

            var user = await _dbContext.Users
                .Find(EmailNormalizer.ByEmail(userInfo.Email))
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                // Create new user for OAuth (password not used for login)
                user = new User
                {
                    Id = await _dbContext.GetNextIdAsync(nameof(User), cancellationToken),
                    Name = userInfo.Name ?? userInfo.Email,
                    Email = EmailNormalizer.Normalize(userInfo.Email),
                    Password = _passwordHasher.HashPassword(Guid.NewGuid().ToString("N")),
                    Active = true
                };
                await _dbContext.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
                _logger.LogInformation("Created user from Google OAuth: {Email}", user.Email);
            }
            else if (!user.Active)
            {
                _logger.LogWarning("Inactive user attempted Google login: {Email}", user.Email);
                return new UserTokenResponse { ErrorCode = LoginErrorCode.AccountInactive };
            }

            var token = await _jwtTokenService.GenerateTokenAsync(user, cancellationToken);
            return new UserTokenResponse { Token = token };
        }

        private sealed class GoogleTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }

        private sealed class GoogleUserInfo
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
            [JsonPropertyName("email")]
            public string? Email { get; set; }
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            [JsonPropertyName("picture")]
            public string? Picture { get; set; }
        }
    }
}
