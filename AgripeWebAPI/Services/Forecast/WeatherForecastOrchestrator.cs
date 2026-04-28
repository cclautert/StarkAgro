using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace AgripeWebAPI.Services.Forecast
{
    public class WeatherForecastOrchestrator : IWeatherForecastService
    {
        private readonly OpenMeteoForecastService _openMeteo;
        private readonly GoogleWeatherAIForecastService _googleWeather;
        private readonly IMemoryCache _cache;
        private readonly WeatherForecastSettings _settings;
        private readonly ILogger<WeatherForecastOrchestrator> _logger;

        public WeatherForecastOrchestrator(
            OpenMeteoForecastService openMeteo,
            GoogleWeatherAIForecastService googleWeather,
            IMemoryCache cache,
            IOptions<WeatherForecastSettings> settings,
            ILogger<WeatherForecastOrchestrator> logger)
        {
            _openMeteo = openMeteo ?? throw new ArgumentNullException(nameof(openMeteo));
            _googleWeather = googleWeather ?? throw new ArgumentNullException(nameof(googleWeather));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, int days, CancellationToken cancellationToken)
        {
            var cacheKey = BuildCacheKey(_settings.PrimarySource, latitude, longitude, days);
            if (_cache.TryGetValue<WeatherForecast>(cacheKey, out var cached) && cached is not null && cached.IsAvailable)
            {
                return cached;
            }

            var result = await TryGetFromAsync(_settings.PrimarySource, latitude, longitude, days, cancellationToken);

            if (!result.IsAvailable && !string.Equals(_settings.FallbackSource, _settings.PrimarySource, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Primary forecast source {Primary} unavailable; falling back to {Fallback}",
                    _settings.PrimarySource, _settings.FallbackSource);

                result = await TryGetFromAsync(_settings.FallbackSource, latitude, longitude, days, cancellationToken);
            }

            if (result.IsAvailable)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Max(1, _settings.CacheDurationMinutes)));
            }

            return result;
        }

        private async Task<WeatherForecast> TryGetFromAsync(string source, double latitude, double longitude, int days, CancellationToken cancellationToken)
        {
            try
            {
                return source switch
                {
                    GoogleWeatherAIForecastService.SourceName => await _googleWeather.GetForecastAsync(latitude, longitude, days, cancellationToken),
                    _ => await _openMeteo.GetForecastAsync(latitude, longitude, days, cancellationToken)
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Forecast source {Source} threw an exception; treating as unavailable.", source);
                return WeatherForecast.Unavailable(source);
            }
        }

        private static string BuildCacheKey(string source, double latitude, double longitude, int days)
        {
            var lat = Math.Round(latitude, 3).ToString("F3", CultureInfo.InvariantCulture);
            var lon = Math.Round(longitude, 3).ToString("F3", CultureInfo.InvariantCulture);
            return $"forecast:{source}:{lat}:{lon}:{days}";
        }
    }
}
