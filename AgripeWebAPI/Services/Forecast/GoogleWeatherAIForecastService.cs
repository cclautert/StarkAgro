using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using Microsoft.Extensions.Options;

namespace AgripeWebAPI.Services.Forecast
{
    public class GoogleWeatherAIForecastService
    {
        public const string SourceName = "GoogleWeatherAI";
        private const string PlaceholderApiKey = "CHANGE_ME";

        private readonly HttpClient _httpClient;
        private readonly WeatherForecastSettings _settings;
        private readonly ILogger<GoogleWeatherAIForecastService> _logger;

        public GoogleWeatherAIForecastService(
            HttpClient httpClient,
            IOptions<WeatherForecastSettings> settings,
            ILogger<GoogleWeatherAIForecastService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, int days, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_settings.GoogleWeatherApiKey) || _settings.GoogleWeatherApiKey == PlaceholderApiKey)
            {
                _logger.LogWarning("GoogleWeatherAI source requested but API key is missing or placeholder; returning unavailable.");
                return Task.FromResult(WeatherForecast.Unavailable(SourceName));
            }

            // Real GraphCast/GenCast integration is out-of-scope for the MVP — this implementation
            // is registered so the source can be selected via config, but currently it short-circuits
            // to "unavailable" so the orchestrator falls through to the fallback source.
            _logger.LogInformation("GoogleWeatherAI source selected; integration not yet implemented, falling through.");
            return Task.FromResult(WeatherForecast.Unavailable(SourceName));
        }
    }
}
