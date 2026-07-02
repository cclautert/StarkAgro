using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace AgripeWebAPI.Services.Forecast
{
    /// <summary>Daily temperature, solar radiation and precipitation used for ET0/moisture projection.</summary>
    public record DailyAgricultureData(
        DateOnly Date,
        double TempMax,
        double TempMin,
        double ShortwaveRadiationMJm2,
        double PrecipitationMm,
        double? PrecipitationProbabilityPct);

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
        /// Fetches daily temperature max/min, shortwave radiation and precipitation for the
        /// next <paramref name="days"/> days — one entry per day, oldest first. Used by the
        /// moisture-prediction algorithm to compute ET0 (Hargreaves simplified) and the rain
        /// offset per projected day. Returns null when the request fails.
        /// </summary>
        public async Task<IReadOnlyList<DailyAgricultureData>?> GetAgricultureDataAsync(
            double latitude, double longitude, int days, CancellationToken cancellationToken)
        {
            var path = $"v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&daily=temperature_2m_max,temperature_2m_min,shortwave_radiation_sum," +
                       $"precipitation_sum,precipitation_probability_max" +
                       $"&forecast_days={days}&timezone=UTC";

            try
            {
                using var response = await _httpClient.GetAsync(path, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("daily", out var daily))
                    return null;

                static double NumericAtOrZero(JsonElement el, string key, int index)
                {
                    if (!el.TryGetProperty(key, out var arr)) return 0;
                    var item = arr.EnumerateArray().ElementAtOrDefault(index);
                    return item.ValueKind == JsonValueKind.Number ? item.GetDouble() : 0;
                }

                static double? NullableNumericAt(JsonElement el, string key, int index)
                {
                    if (!el.TryGetProperty(key, out var arr)) return null;
                    var item = arr.EnumerateArray().ElementAtOrDefault(index);
                    return item.ValueKind == JsonValueKind.Number ? item.GetDouble() : null;
                }

                var times = daily.TryGetProperty("time", out var timeEl)
                    ? timeEl.EnumerateArray().Select(e => e.GetString()).ToList()
                    : new List<string?>();

                var result = new List<DailyAgricultureData>(times.Count);
                for (int i = 0; i < times.Count; i++)
                {
                    if (string.IsNullOrEmpty(times[i]) ||
                        !DateOnly.TryParse(times[i], CultureInfo.InvariantCulture, out var date))
                    {
                        continue;
                    }

                    var tMax = NumericAtOrZero(daily, "temperature_2m_max", i);
                    var tMin = NumericAtOrZero(daily, "temperature_2m_min", i);
                    var rad = NumericAtOrZero(daily, "shortwave_radiation_sum", i);
                    var precip = NumericAtOrZero(daily, "precipitation_sum", i);
                    var precipProb = NullableNumericAt(daily, "precipitation_probability_max", i);

                    result.Add(new DailyAgricultureData(date, tMax, tMin, rad, precip, precipProb));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenMeteo agriculture data unavailable for {Lat},{Lon}", latitude, longitude);
                return null;
            }
        }

        /// <summary>
        /// Accumulated precipitation (mm) over the last <paramref name="pastDays"/> days plus
        /// today. Used to suppress high-humidity sensor anomalies during rainy periods.
        /// </summary>
        public async Task<double?> GetRecentPrecipitationAsync(
            double latitude, double longitude, int pastDays, CancellationToken cancellationToken)
        {
            var path = $"v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                       $"&daily=precipitation_sum&past_days={pastDays}&forecast_days=1&timezone=UTC";

            try
            {
                using var response = await _httpClient.GetAsync(path, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("daily", out var daily) ||
                    !daily.TryGetProperty("precipitation_sum", out var precipEl))
                    return null;

                double total = 0;
                foreach (var item in precipEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number)
                        total += item.GetDouble();
                }

                return Math.Round(total, 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenMeteo recent precipitation unavailable for {Lat},{Lon}", latitude, longitude);
                return null;
            }
        }
    }
}
