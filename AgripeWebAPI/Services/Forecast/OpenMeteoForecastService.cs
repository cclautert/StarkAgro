using AgripeWebAPI.Models;
using System.Globalization;
using System.Text.Json;

namespace AgripeWebAPI.Services.Forecast
{
    public class OpenMeteoForecastService
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
    }
}
