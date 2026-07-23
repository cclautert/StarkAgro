using Microsoft.Extensions.Logging;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StarkAgroAPI.Services.Sentinel1
{
    /// <param name="ValidSampleCount">Pixels válidos (dataMask=1). Zero = passagem sem dado útil.</param>
    public record Sentinel1Stat(
        DateTime AcquisitionDate,
        double RviMean,
        double VvMean,
        double VhMean,
        long ValidSampleCount);

    /// <summary>
    /// Statistical API da CDSE para <b>Sentinel-1 GRD</b> (radar): série de RVI + VV/VH sobre o
    /// polígono da área. Serviço <b>separado</b> do NDVI de propósito — o S1 tem evalscript,
    /// <c>dataFilter</c> (sem nuvem) e parsing próprios; parametrizar o serviço do NDVI o poluiria e
    /// arriscaria o caminho S2. Parsing defensivo, <c>catch → null</c>, como o do NDVI.
    /// </summary>
    public interface ICdseSentinel1Service
    {
        /// <param name="orbitDirection">Fixo (<c>DESCENDING</c>): misturar asc/desc faz o backscatter
        /// pular por geometria de visada, não por lavoura.</param>
        Task<IReadOnlyList<Sentinel1Stat>?> GetStatisticsAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            string orbitDirection,
            CancellationToken cancellationToken);
    }

    public class CdseSentinel1Service : ICdseSentinel1Service
    {
        private const string Endpoint = "api/v1/statistics";

        // RVI = 4·VH/(VV+VH) — índice de vegetação por radar (GAMMA0 linear). VV/VH crus para contexto.
        // dataMask exclui no-data. Sem SCL/nuvem: radar atravessa nuvem, esse é o ponto.
        private const string Evalscript = """
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: ["VV", "VH", "dataMask"] }],
                output: [
                  { id: "rvi", bands: 1 }, { id: "vv", bands: 1 },
                  { id: "vh", bands: 1 }, { id: "dataMask", bands: 1 }
                ]
              };
            }
            function evaluatePixel(s) {
              let denom = s.VV + s.VH;
              let rvi = denom > 0 ? (4 * s.VH / denom) : 0;
              return { rvi: [rvi], vv: [s.VV], vh: [s.VH], dataMask: [s.dataMask] };
            }
            """;

        private readonly HttpClient _httpClient;
        private readonly ILogger<CdseSentinel1Service> _logger;

        public CdseSentinel1Service(HttpClient httpClient, ILogger<CdseSentinel1Service> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<Sentinel1Stat>?> GetStatisticsAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            string orbitDirection,
            CancellationToken cancellationToken)
        {
            try
            {
                var body = BuildRequestBody(geometry, from, to, orbitDirection);
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("CDSE S1: HTTP {Status} — {Body}", (int)response.StatusCode, Truncate(err));
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return Parse(doc.RootElement);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CDSE S1 request failed");
                return null;
            }
        }

        public static string BuildRequestBody(
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry, DateTime from, DateTime to, string orbitDirection)
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
                        new
                        {
                            type = "sentinel-1-grd",
                            // Sem maxCloudCoverage — radar não tem nuvem. Órbita FIXA para a série
                            // não misturar geometrias de visada.
                            dataFilter = new
                            {
                                acquisitionMode = "IW",
                                polarization = "DV",
                                orbitDirection
                            },
                            processing = new { backCoeff = "GAMMA0_ELLIPSOID" }
                        }
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

        public static IReadOnlyList<Sentinel1Stat> Parse(JsonElement root)
        {
            var result = new List<Sentinel1Stat>();
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var interval in data.EnumerateArray())
            {
                if (!interval.TryGetProperty("interval", out var iv)
                    || !iv.TryGetProperty("from", out var fromEl)
                    || fromEl.ValueKind != JsonValueKind.String
                    || !DateTime.TryParse(fromEl.GetString(), CultureInfo.InvariantCulture,
                           DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
                {
                    continue;
                }

                var rvi = Stat(interval, "rvi");
                if (rvi is null) continue; // intervalo sem passagem S1 — buraco honesto, não zero falso

                result.Add(new Sentinel1Stat(
                    date,
                    rvi.Value.mean,
                    Stat(interval, "vv")?.mean ?? 0,
                    Stat(interval, "vh")?.mean ?? 0,
                    rvi.Value.sampleCount));
            }

            return result;
        }

        private static (double mean, long sampleCount)? Stat(JsonElement interval, string outputId)
        {
            if (!interval.TryGetProperty("outputs", out var outputs)
                || !outputs.TryGetProperty(outputId, out var output)
                || !output.TryGetProperty("bands", out var bands)
                || !bands.TryGetProperty("B0", out var b0)
                || !b0.TryGetProperty("stats", out var s))
            {
                return null;
            }

            var sampleCount = s.TryGetProperty("sampleCount", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetInt64() : 0;
            if (sampleCount == 0) return null;
            var mean = s.TryGetProperty("mean", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetDouble() : 0;
            return (mean, sampleCount);
        }

        private static string Truncate(string s) => s.Length <= 300 ? s : s[..300];
    }
}
