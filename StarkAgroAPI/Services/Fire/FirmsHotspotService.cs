using Microsoft.Extensions.Logging;
using StarkAgroAPI.Services.Ndvi;
using System.Globalization;

namespace StarkAgroAPI.Services.Fire
{
    /// <param name="AcquiredAt">Momento da passagem em UTC (de acq_date + acq_time).</param>
    public record FireHotspotDto(
        double Latitude,
        double Longitude,
        DateTime AcquiredAt,
        string Satellite,
        string Confidence,
        double Frp);

    /// <summary>
    /// Focos de calor do NASA FIRMS (Fire Information for Resource Management System) num bbox.
    /// Resposta é <b>CSV</b>, não JSON. Parsing defensivo, <c>catch → null</c> (mesma disciplina de
    /// <see cref="CdseStatisticalService"/>). API gratuita — zero Processing Unit.
    /// </summary>
    public interface IFirmsHotspotService
    {
        /// <param name="source">Fonte FIRMS, ex.: <c>VIIRS_SNPP_NRT</c>.</param>
        /// <param name="bbox">Bbox <c>[minLng, minLat, maxLng, maxLat]</c> — o FIRMS quer W,S,E,N.</param>
        /// <param name="dayRange">Janela em dias (1–10).</param>
        Task<IReadOnlyList<FireHotspotDto>?> GetHotspotsAsync(
            string mapKey, string source, NdviBbox bbox, int dayRange, CancellationToken cancellationToken);
    }

    public class FirmsHotspotService : IFirmsHotspotService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FirmsHotspotService> _logger;

        public FirmsHotspotService(HttpClient httpClient, ILogger<FirmsHotspotService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<FireHotspotDto>?> GetHotspotsAsync(
            string mapKey, string source, NdviBbox bbox, int dayRange, CancellationToken cancellationToken)
        {
            try
            {
                // /api/area/csv/{key}/{source}/{W,S,E,N}/{days}
                var area = string.Create(CultureInfo.InvariantCulture,
                    $"{bbox.MinLng},{bbox.MinLat},{bbox.MaxLng},{bbox.MaxLat}");
                var url = $"api/area/csv/{mapKey}/{source}/{area}/{dayRange}";

                using var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("FIRMS: HTTP {Status} — {Body}", (int)response.StatusCode, Truncate(err));
                    return null;
                }

                var csv = await response.Content.ReadAsStringAsync(cancellationToken);

                // O FIRMS devolve 200 com um corpo de ERRO em texto quando a MAP_KEY é inválida ou o
                // rate limit estourou (ex.: "Invalid MAP_KEY..."). Sem cabeçalho CSV → trata como falha.
                if (csv.Contains("Invalid MAP_KEY", StringComparison.OrdinalIgnoreCase)
                    || csv.Contains("MAP_KEY", StringComparison.OrdinalIgnoreCase) && !csv.Contains("latitude", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("FIRMS: resposta de erro em texto — {Body}", Truncate(csv));
                    return null;
                }

                return ParseCsv(csv);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "FIRMS request failed");
                return null;
            }
        }

        /// <summary>
        /// CSV do FIRMS → focos. <b>Mapeia por NOME de coluna</b> (linha de cabeçalho), nunca por
        /// posição: assim reordenar ou adicionar coluna não desalinha o parse, e uma coluna faltando
        /// é detectada em vez de ler o campo errado. Cabeçalho ausente ou CSV vazio → lista vazia.
        /// Linha com número de campos diferente do cabeçalho, ou lat/lng/data inválidos, é pulada.
        /// </summary>
        public static IReadOnlyList<FireHotspotDto> ParseCsv(string csv)
        {
            var result = new List<FireHotspotDto>();
            if (string.IsNullOrWhiteSpace(csv)) return result;

            var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return result; // só cabeçalho, ou nada

            var header = lines[0].Split(',');
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Length; i++)
                col[header[i].Trim()] = i;

            // Colunas obrigatórias para um foco fazer sentido. Faltando qualquer uma → não dá para
            // parsear com segurança; devolve vazio em vez de inventar dado.
            if (!col.ContainsKey("latitude") || !col.ContainsKey("longitude")
                || !col.ContainsKey("acq_date") || !col.ContainsKey("acq_time"))
            {
                return result;
            }

            foreach (var line in lines.Skip(1))
            {
                var f = line.Split(',');
                if (f.Length != header.Length) continue; // linha desalinhada — pula

                if (!TryDouble(f, col, "latitude", out var lat)) continue;
                if (!TryDouble(f, col, "longitude", out var lng)) continue;
                if (!TryParseAcquired(Get(f, col, "acq_date"), Get(f, col, "acq_time"), out var acquired)) continue;

                TryDouble(f, col, "frp", out var frp); // opcional: default 0

                result.Add(new FireHotspotDto(
                    lat, lng, acquired,
                    Get(f, col, "satellite"),
                    Get(f, col, "confidence"),
                    frp));
            }

            return result;
        }

        // acq_date "YYYY-MM-DD" + acq_time "HHMM"/"HMM"/"MM"/"M" (UTC) → DateTime UTC.
        private static bool TryParseAcquired(string date, string time, out DateTime acquired)
        {
            acquired = default;
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
            {
                return false;
            }

            // acq_time vem sem zero à esquerda ("742" = 07:42). Normaliza para 4 dígitos.
            var t = new string(time.Where(char.IsDigit).ToArray()).PadLeft(4, '0');
            if (t.Length != 4) return false;
            if (!int.TryParse(t[..2], out var hh) || !int.TryParse(t[2..], out var mm)) return false;
            if (hh > 23 || mm > 59) return false;

            acquired = new DateTime(d.Year, d.Month, d.Day, hh, mm, 0, DateTimeKind.Utc);
            return true;
        }

        private static string Get(string[] fields, Dictionary<string, int> col, string name) =>
            col.TryGetValue(name, out var i) && i < fields.Length ? fields[i].Trim() : string.Empty;

        private static bool TryDouble(string[] fields, Dictionary<string, int> col, string name, out double value) =>
            double.TryParse(Get(fields, col, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static string Truncate(string s) => s.Length <= 300 ? s : s[..300];
    }
}
