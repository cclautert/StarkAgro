using Microsoft.Extensions.Logging;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <param name="ValidSampleCount">Pixels válidos (não-nuvem/no-data). Zero = passagem toda nublada.</param>
    /// <param name="Mean">NDVI médio — o índice primário (existe em toda passagem, incl. legado).</param>
    public record NdviStat(
        DateTime AcquisitionDate,
        double Mean,
        double Min,
        double Max,
        double Stdev,
        long ValidSampleCount,
        double CloudPct)
    {
        /// <summary>
        /// Pixels por classe de <see cref="NdviClassification"/>, na ordem de
        /// <see cref="NdviClassification.Classes"/>. Vem do histograma da <b>mesma</b> requisição
        /// Statistical (sem Processing Unit extra). Vazio quando a resposta não trouxe histograma.
        /// </summary>
        public IReadOnlyList<long> ClassCounts { get; init; } = [];

        // Índices extras (F1). Zero quando a passagem foi buscada sem ExtraIndicesEnabled — o
        // consumidor distingue "sem dado" por data/nuvem, nunca pelo valor zero (vegetação real
        // pode ter NDRE ~0). Ctor posicional deixa só o NDVI para os call-sites de antes desta fase.
        public double NdreMean { get; init; }
        public double NdreMin { get; init; }
        public double NdreMax { get; init; }
        public double NdreStdev { get; init; }
        public double NdmiMean { get; init; }
        public double NdmiMin { get; init; }
        public double NdmiMax { get; init; }
        public double NdmiStdev { get; init; }
    }

    /// <summary>
    /// Statistical API da CDSE (Sentinel Hub): série de estatísticas de NDVI sobre o polígono da
    /// área, uma por passagem, com filtro de nuvem no servidor. Parsing defensivo, <c>catch → null</c>.
    /// </summary>
    public interface ICdseStatisticalService
    {
        /// <param name="extraIndices">
        /// <c>true</c> pede NDRE (B05) e NDMI (B11) além do NDVI, numa única requisição (6 bandas
        /// de entrada → fator PU 2,0). <c>false</c> mantém o request de sempre (4 bandas, só NDVI).
        /// </param>
        Task<IReadOnlyList<NdviStat>?> GetStatisticsAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            bool extraIndices,
            CancellationToken cancellationToken);
    }

    public class CdseStatisticalService : ICdseStatisticalService
    {
        private const string Endpoint = "api/v1/statistics";

        // Só NDVI (4 bandas). Comportamento de antes da F1 — o request quando ExtraIndicesEnabled=off.
        // NDVI = (B08-B04)/(B08+B04); dataMask exclui nuvem (SCL 3/8/9/10) e no-data.
        private const string EvalscriptNdvi = """
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

        // NDVI + NDRE (B05, red-edge, não satura em dossel denso) + NDMI (B11, SWIR, umidade do
        // dossel). 6 bandas de entrada → fator PU 2,0. As três saídas compartilham a MESMA máscara
        // de nuvem/no-data, então uma passagem nublada zera as três de forma consistente.
        //   NDVI = (B08-B04)/(B08+B04)   NDRE = (B08-B05)/(B08+B05)   NDMI = (B08-B11)/(B08+B11)
        private const string EvalscriptExtra = """
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: ["B04", "B05", "B08", "B11", "SCL", "dataMask"] }],
                output: [
                  { id: "ndvi", bands: 1 }, { id: "ndre", bands: 1 },
                  { id: "ndmi", bands: 1 }, { id: "dataMask", bands: 1 }
                ]
              };
            }
            function evaluatePixel(s) {
              let ndvi = (s.B08 - s.B04) / (s.B08 + s.B04);
              let ndre = (s.B08 - s.B05) / (s.B08 + s.B05);
              let ndmi = (s.B08 - s.B11) / (s.B08 + s.B11);
              let cloud = (s.SCL === 3 || s.SCL === 8 || s.SCL === 9 || s.SCL === 10);
              let valid = (s.dataMask === 1 && !cloud) ? 1 : 0;
              return { ndvi: [ndvi], ndre: [ndre], ndmi: [ndmi], dataMask: [valid] };
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
            bool extraIndices,
            CancellationToken cancellationToken)
        {
            try
            {
                var body = BuildRequestBody(geometry, from, to, extraIndices);
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
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry, DateTime from, DateTime to, bool extraIndices)
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
                    evalscript = extraIndices ? EvalscriptExtra : EvalscriptNdvi,
                    resx = 10,
                    resy = 10
                },
                // Histograma na MESMA requisição — a contagem por faixa sai sem Processing Unit extra.
                // Histograma UNIFORME (nBins/lowEdge/highEdge), nunca array de arestas: a CDSE
                // responde 400 COMMON_BAD_PAYLOAD a `bins` explícito. Só o de NDVI é agregado em
                // classes (ParseHistogram); os de NDRE/NDMI vêm na resposta e ficam guardados para
                // quando essas faixas forem definidas.
                calculations = BuildCalculations(extraIndices)
            };

            return JsonSerializer.Serialize(payload);
        }

        // Um bloco de histograma por saída. `default` cobre todas as bandas do output (aqui só B0).
        private static Dictionary<string, object> BuildCalculations(bool extraIndices)
        {
            var hist = new
            {
                histograms = new
                {
                    @default = new
                    {
                        nBins = NdviClassification.HistogramBinCount,
                        lowEdge = NdviClassification.HistogramLowEdge,
                        highEdge = NdviClassification.HistogramHighEdge
                    }
                }
            };

            var calc = new Dictionary<string, object> { ["ndvi"] = hist };
            if (extraIndices)
            {
                calc["ndre"] = hist;
                calc["ndmi"] = hist;
            }
            return calc;
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

                var stats = TryGetStats(interval, "ndvi");
                if (stats is null)
                {
                    // Intervalo sem dado válido (ex.: totalmente nublado) — buraco honesto na série.
                    result.Add(new NdviStat(date, 0, 0, 0, 0, 0, 100));
                    continue;
                }

                var (mean, min, max, stdev, sampleCount, noData) = stats.Value;
                var valid = Math.Max(0, sampleCount - noData);
                var cloudPct = sampleCount > 0 ? 100.0 * noData / sampleCount : 100.0;

                // NDRE/NDMI: presentes só quando a passagem foi buscada com ExtraIndicesEnabled.
                // Output ausente → default zero (o `?? default` do value tuple), nunca exceção.
                var ndre = TryGetStats(interval, "ndre") ?? default;
                var ndmi = TryGetStats(interval, "ndmi") ?? default;

                result.Add(new NdviStat(date, mean, min, max, stdev, valid, cloudPct)
                {
                    ClassCounts = ParseHistogram(interval),
                    NdreMean = ndre.mean, NdreMin = ndre.min, NdreMax = ndre.max, NdreStdev = ndre.stdev,
                    NdmiMean = ndmi.mean, NdmiMin = ndmi.min, NdmiMax = ndmi.max, NdmiStdev = ndmi.stdev
                });
            }

            return result;
        }

        /// <summary>
        /// Contagem de pixels por classe de biomassa, agregada do histograma uniforme em
        /// <c>outputs.ndvi.bands.B0.histogram.bins</c>.
        /// <para>
        /// Cada bin fino é atribuído a uma classe pelo seu <b>ponto médio</b>, nunca por
        /// igualdade de aresta: <c>lowEdge</c> vem da CDSE como double e uma comparação exata
        /// com 0,35 quebraria por ruído de ponto flutuante, jogando pixels na classe vizinha.
        /// </para>
        /// Defensivo como o resto do parser: histograma ausente devolve lista vazia e a tela cai
        /// no fallback de "sem classificação", em vez de mostrar distribuição inventada.
        /// </summary>
        public static IReadOnlyList<long> ParseHistogram(JsonElement interval)
        {
            if (!interval.TryGetProperty("outputs", out var outputs)
                || !outputs.TryGetProperty("ndvi", out var ndvi)
                || !ndvi.TryGetProperty("bands", out var bands)
                || !bands.TryGetProperty("B0", out var b0)
                || !b0.TryGetProperty("histogram", out var histogram)
                || !histogram.TryGetProperty("bins", out var bins)
                || bins.ValueKind != JsonValueKind.Array
                || bins.GetArrayLength() == 0)
            {
                return [];
            }

            var counts = new long[NdviClassification.Classes.Count];
            var sawAny = false;

            foreach (var bin in bins.EnumerateArray())
            {
                if (!bin.TryGetProperty("lowEdge", out var lo) || lo.ValueKind != JsonValueKind.Number)
                    continue;
                if (!bin.TryGetProperty("count", out var c) || c.ValueKind != JsonValueKind.Number)
                    continue;

                // highEdge pode faltar no último bin de algumas respostas: cai para a largura nominal.
                var low = lo.GetDouble();
                var high = bin.TryGetProperty("highEdge", out var hi) && hi.ValueKind == JsonValueKind.Number
                    ? hi.GetDouble()
                    : low + BinWidth;

                var index = NdviClassification.ClassIndexFor((low + high) / 2.0);
                if (index < 0) continue;   // bin fora do domínio do NDVI — ignorado, não somado errado

                counts[index] += c.GetInt64();
                sawAny = true;
            }

            return sawAny ? counts : [];
        }

        private static double BinWidth =>
            (double)(NdviClassification.HistogramHighEdge - NdviClassification.HistogramLowEdge)
            / NdviClassification.HistogramBinCount;

        private static (double mean, double min, double max, double stdev, long sampleCount, long noData)? TryGetStats(
            JsonElement interval, string outputId)
        {
            if (!interval.TryGetProperty("outputs", out var outputs)
                || !outputs.TryGetProperty(outputId, out var output)
                || !output.TryGetProperty("bands", out var bands)
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
