using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace AgripeWebAPI.Services.Forecast
{
    /// <summary>Temperature + solar radiation snapshot used for ET0 calculation.</summary>
    public record AgricultureWeatherData(double TempMax, double TempMin, double ShortwaveRadiationMJm2);

    public class OpenMeteoForecastService : IAgricultureWeatherService
    {
        public const string SourceName = "OpenMeteo";

        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenMeteoForecastService> _logger;

        public OpenMeteoForecastService(HttpClient httpClient, ILogger<OpenMeteoForecastService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, int days, CancellationToken cancellationToken)
        {
            var path = $"v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&daily=precipitation_sum,precipitation_probability_max" +
                       $"&forecast_days={days}&timezone=UTC";

            using var response = await _httpClient.GetAsync(path, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("daily", out var daily))
            {
                _logger.LogWarning("OpenMeteo response missing 'daily' block for {Latitude},{Longitude}", latitude, longitude);
                return WeatherForecast.Unavailable(SourceName);
            }

            var times = daily.GetProperty("time").EnumerateArray().Select(e => e.GetString()).ToList();
            var precip = daily.GetProperty("precipitation_sum").EnumerateArray().ToList();
            var probability = daily.TryGetProperty("precipitation_probability_max", out var probEl)
                ? probEl.EnumerateArray().ToList()
                : null;

            var forecasts = new List<DailyForecast>(times.Count);
            double total = 0;
            double? avgProbability = null;
            int probSamples = 0;
            double probSum = 0;

            for (int i = 0; i < times.Count; i++)
            {
                var dateStr = times[i];
                if (string.IsNullOrEmpty(dateStr) || !DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, out var date))
                {
                    continue;
                }

                var mm = precip.Count > i && precip[i].ValueKind == JsonValueKind.Number ? precip[i].GetDouble() : 0;
                double? prob = null;
                if (probability != null && probability.Count > i && probability[i].ValueKind == JsonValueKind.Number)
                {
                    prob = probability[i].GetDouble();
                    probSum += prob.Value;
                    probSamples++;
                }

                forecasts.Add(new DailyForecast(date, mm, prob));
                total += mm;
            }

            if (probSamples > 0)
            {
                avgProbability = probSum / probSamples;
            }

            return new WeatherForecast
            {
                TotalPrecipitationMm = Math.Round(total, 2),
                DailyForecasts = forecasts,
                Source = SourceName,
                ProbabilityOfPrecipitation = avgProbability,
                IsAvailable = true
            };
        }

        /// <summary>
        /// Fetches daily temperature max/min and shortwave radiation for the next
        /// <paramref name="days"/> days. Used by the moisture-prediction algorithm to
        /// compute ET0 (Hargreaves simplified). Returns null when the request fails.
        /// </summary>
        public async Task<AgricultureWeatherData?> GetAgricultureDataAsync(
            double latitude, double longitude, int days, CancellationToken cancellationToken)
        {
            var path = $"v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&daily=temperature_2m_max,temperature_2m_min,shortwave_radiation_sum" +
                       $"&forecast_days={days}&timezone=UTC";

            try
            {
                using var response = await _httpClient.GetAsync(path, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("daily", out var daily))
                    return null;

                static double FirstNumericOrZero(JsonElement el, string key)
                {
                    if (!el.TryGetProperty(key, out var arr)) return 0;
                    var first = arr.EnumerateArray().FirstOrDefault();
                    return first.ValueKind == JsonValueKind.Number ? first.GetDouble() : 0;
                }

                var tMax = FirstNumericOrZero(daily, "temperature_2m_max");
                var tMin = FirstNumericOrZero(daily, "temperature_2m_min");
                var rad = FirstNumericOrZero(daily, "shortwave_radiation_sum");

                return new AgricultureWeatherData(tMax, tMin, rad);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenMeteo agriculture data unavailable for {Lat},{Lon}", latitude, longitude);
                return null;
            }
        }
    }
}
