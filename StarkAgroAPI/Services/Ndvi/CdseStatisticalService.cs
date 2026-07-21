using Microsoft.Extensions.Logging;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <param name="ValidSampleCount">Pixels válidos (não-nuvem/no-data). Zero = passagem toda nublada.</param>
    public record NdviStat(
        DateTime AcquisitionDate,
        double Mean,
        double Min,
        double Max,
        double Stdev,
        long ValidSampleCount,
        double CloudPct);

    /// <summary>
    /// Statistical API da CDSE (Sentinel Hub): série de estatísticas de NDVI sobre o polígono da
    /// área, uma por passagem, com filtro de nuvem no servidor. Parsing defensivo, <c>catch → null</c>.
    /// </summary>
    public interface ICdseStatisticalService
    {
        Task<IReadOnlyList<NdviStat>?> GetStatisticsAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken);
    }

    public class CdseStatisticalService : ICdseStatisticalService
    {
        private const string Endpoint = "api/v1/statistics";

        // Evalscript: NDVI = (B08-B04)/(B08+B04); dataMask exclui nuvem (SCL 3/8/9/10) e no-data.
        private const string Evalscript = """
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: ["B04", "B08", "SCL", "dataMask"] }],
                output: [{ id: "ndvi", bands: 1 }, { id: "dataMask", bands: 1 }]
              };
            }
            function evaluatePixel(s) {
              let ndvi = (s.B08 - s.B04) / (s.B08 + s.B04);
              let cloud = (s.SCL === 3 || s.SCL === 8 || s.SCL === 9 || s.SCL === 10);
              let valid = (s.dataMask === 1 && !cloud) ? 1 : 0;
              return { ndvi: [ndvi], dataMask: [valid] };
            }
            """;

        private readonly HttpClient _httpClient;
        private readonly ILogger<CdseStatisticalService> _logger;

        public CdseStatisticalService(HttpClient httpClient, ILogger<CdseStatisticalService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<NdviStat>?> GetStatisticsAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            try
            {
                var body = BuildRequestBody(geometry, from, to);
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("CDSE statistics: HTTP {Status} — {Body}", (int)response.StatusCode, Truncate(err));
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return Parse(doc.RootElement);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CDSE statistics request failed");
                return null;
            }
        }

        public static string BuildRequestBody(
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry, DateTime from, DateTime to)
        {
            var ring = geometry.Coordinates.Exterior.Positions
                .Select(p => new[] { p.Longitude, p.Latitude })
                .ToArray();

            var payload = new
            {
                input = new
                {
                    bounds = new
                    {
                        geometry = new { type = "Polygon", coordinates = new[] { ring } },
                        properties = new { crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84" }
                    },
                    data = new[]
                    {
                        new { type = "sentinel-2-l2a", dataFilter = new { maxCloudCoverage = 80 } }
                    }
                },
                aggregation = new
                {
                    timeRange = new
                    {
                        from = from.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                        to = to.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                    },
                    aggregationInterval = new { of = "P1D" },
                    evalscript = Evalscript,
                    resx = 10,
                    resy = 10
                }
            };

            return JsonSerializer.Serialize(payload);
        }

        public static IReadOnlyList<NdviStat> Parse(JsonElement root)
        {
            var result = new List<NdviStat>();
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var interval in data.EnumerateArray())
            {
                if (!interval.TryGetProperty("interval", out var iv)
                    || !iv.TryGetProperty("from", out var fromEl)
                    || !fromEl.TryGetString(out var fromStr)
                    || !DateTime.TryParse(fromStr, CultureInfo.InvariantCulture,
                           DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
                {
                    continue;
                }

                var stats = TryGetStats(interval);
                if (stats is null)
                {
                    // Intervalo sem dado válido (ex.: totalmente nublado) — buraco honesto na série.
                    result.Add(new NdviStat(date, 0, 0, 0, 0, 0, 100));
                    continue;
                }

                var (mean, min, max, stdev, sampleCount, noData) = stats.Value;
                var valid = Math.Max(0, sampleCount - noData);
                var cloudPct = sampleCount > 0 ? 100.0 * noData / sampleCount : 100.0;
                result.Add(new NdviStat(date, mean, min, max, stdev, valid, cloudPct));
            }

            return result;
        }

        private static (double mean, double min, double max, double stdev, long sampleCount, long noData)? TryGetStats(JsonElement interval)
        {
            if (!interval.TryGetProperty("outputs", out var outputs)
                || !outputs.TryGetProperty("ndvi", out var ndvi)
                || !ndvi.TryGetProperty("bands", out var bands)
                || !bands.TryGetProperty("B0", out var b0)
                || !b0.TryGetProperty("stats", out var s))
            {
                return null;
            }

            double D(string name) => s.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
            long L(string name) => s.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

            var sampleCount = L("sampleCount");
            if (sampleCount == 0) return null;
            return (D("mean"), D("min"), D("max"), D("stDev"), sampleCount, L("noDataCount"));
        }

        private static string Truncate(string s) => s.Length <= 300 ? s : s[..300];
    }

    internal static class JsonElementExtensions
    {
        public static bool TryGetString(this JsonElement el, out string value)
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                value = el.GetString() ?? string.Empty;
                return true;
            }
            value = string.Empty;
            return false;
        }
    }
}
