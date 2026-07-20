using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Token OAuth2 client-credentials da CDSE (Copernicus), com cache em memória. O token é curto;
    /// guardamos por <c>expires_in</c> menos uma margem — mesmo padrão de cache do orquestrador de tempo.
    /// </summary>
    public interface ICdseTokenProvider
    {
        Task<string?> GetTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken);
    }

    public class CdseTokenProvider : ICdseTokenProvider
    {
        // Endpoint de identidade (Keycloak) da CDSE — relativo à BaseAddress do HttpClient.
        private const string TokenPath = "auth/realms/CDSE/protocol/openid-connect/token";
        private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(60);

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CdseTokenProvider> _logger;

        public CdseTokenProvider(HttpClient httpClient, IMemoryCache cache, ILogger<CdseTokenProvider> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> GetTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            var cacheKey = $"cdse_token:{clientId}";
            if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
                return cached;

            try
            {
                using var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                });

                using var response = await _httpClient.PostAsync(TokenPath, form, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CDSE token: HTTP {Status}", (int)response.StatusCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (!root.TryGetProperty("access_token", out var tokenEl)) return null;
                var token = tokenEl.GetString();
                if (string.IsNullOrEmpty(token)) return null;

                var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var secs) ? secs : 600;
                var ttl = TimeSpan.FromSeconds(expiresIn) - ExpiryMargin;
                if (ttl > TimeSpan.Zero) _cache.Set(cacheKey, token, ttl);

                return token;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CDSE token request failed");
                return null;
            }
        }
    }
}
