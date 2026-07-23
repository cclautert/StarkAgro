using Microsoft.Extensions.Logging;
using MongoDB.Driver.GeoJsonObjectModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Bounding box geográfico da área, ordem <c>[minLng, minLat, maxLng, maxLat]</c> — a mesma
    /// caixa em que a Process API renderiza o PNG e em que o front desenha o <c>L.imageOverlay</c>.
    /// </summary>
    public record NdviBbox(double MinLng, double MinLat, double MaxLng, double MaxLat)
    {
        public double[] ToArray() => [MinLng, MinLat, MaxLng, MaxLat];
    }

    /// <summary>
    /// Process API da CDSE (Sentinel Hub): renderiza um PNG colorizado do NDVI no bbox da área,
    /// para servir como overlay no mapa. Parsing/erro defensivo, <c>catch → null</c> (mesma
    /// disciplina de <see cref="CdseStatisticalService"/>). O overlay é acessório — quem falhar
    /// aqui não pode derrubar a busca de tendência.
    /// </summary>
    public interface ICdseProcessService
    {
        Task<byte[]?> GetNdviOverlayPngAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken);

        /// <summary>GeoTIFF de zonas (classe por pixel, 8 bits, georreferenciado). <c>null</c> em falha.</summary>
        Task<byte[]?> GetNdviZonesTiffAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken);
    }

    public class CdseProcessService : ICdseProcessService
    {
        private const string Endpoint = "api/v1/process";

        // Lado maior do PNG em pixels: o overlay é redimensionado no mapa, então não precisa
        // de resolução nativa — teto controla o custo de Processing Units da renderização.
        private const int MaxDimension = 512;

        /// <summary>
        /// Evalscript v3: NDVI colorizado com alpha; nuvem/no-data → transparente. O <c>ramp()</c>
        /// é <b>gerado</b> a partir de <see cref="NdviClassification"/>, não escrito à mão: as cores
        /// do PNG e as da legenda saem da mesma lista, então um corte novo move as duas juntas.
        /// </summary>
        public static readonly string Evalscript = $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: ["B04", "B08", "SCL", "dataMask"] }],
                output: { bands: 4 }
              };
            }
            {{NdviClassification.BuildRampFunction()}}
            function evaluatePixel(s) {
              let cloud = (s.SCL === 3 || s.SCL === 8 || s.SCL === 9 || s.SCL === 10);
              if (s.dataMask === 0 || cloud) return [0, 0, 0, 0];
              let ndvi = (s.B08 - s.B04) / (s.B08 + s.B04);
              let c = ramp(ndvi);
              return [c[0], c[1], c[2], 1];
            }
            """;

        /// <summary>
        /// Evalscript v3 de <b>zonas</b>: GeoTIFF <b>colorido</b> (RGB) com as cores das classes de
        /// biomassa — abre legível em qualquer visualizador E no QGIS, mantendo o georreferenciamento.
        /// Reusa o <c>ramp()</c> de <see cref="NdviClassification"/> (as mesmas cores do overlay e da
        /// legenda). Fora do talhão / nuvem → branco, para o arquivo ficar limpo sobre fundo claro.
        /// </summary>
        public static readonly string ZonesEvalscript = $$"""
            //VERSION=3
            function setup() {
              return {
                input: [{ bands: ["B04", "B08", "SCL", "dataMask"] }],
                output: { bands: 3 }
              };
            }
            {{NdviClassification.BuildRampFunction()}}
            function evaluatePixel(s) {
              let cloud = (s.SCL === 3 || s.SCL === 8 || s.SCL === 9 || s.SCL === 10);
              if (s.dataMask === 0 || cloud) return [1, 1, 1];
              let ndvi = (s.B08 - s.B04) / (s.B08 + s.B04);
              return ramp(ndvi);
            }
            """;

        private readonly HttpClient _httpClient;
        private readonly ILogger<CdseProcessService> _logger;

        public CdseProcessService(HttpClient httpClient, ILogger<CdseProcessService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<byte[]?> GetNdviOverlayPngAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            try
            {
                var bbox = ComputeBbox(geometry);
                var (width, height) = ResolveDimensions(bbox);
                var body = BuildRequestBody(geometry, from, to, width, height);

                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("CDSE process: HTTP {Status} — {Body}", (int)response.StatusCode, Truncate(err));
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CDSE process request failed");
                return null;
            }
        }

        public async Task<byte[]?> GetNdviZonesTiffAsync(
            string token,
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            try
            {
                var bbox = ComputeBbox(geometry);
                var (width, height) = ResolveDimensions(bbox);
                var body = BuildZonesRequestBody(geometry, from, to, width, height);

                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/tiff"));

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("CDSE zones: HTTP {Status} — {Body}", (int)response.StatusCode, Truncate(err));
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CDSE zones request failed");
                return null;
            }
        }

        /// <summary>Bbox do anel exterior do polígono (ordem <c>[lng,lat]</c> do GeoJSON).</summary>
        public static NdviBbox ComputeBbox(GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry)
        {
            var positions = geometry.Coordinates.Exterior.Positions;
            double minLng = double.MaxValue, minLat = double.MaxValue, maxLng = double.MinValue, maxLat = double.MinValue;
            foreach (var p in positions)
            {
                if (p.Longitude < minLng) minLng = p.Longitude;
                if (p.Longitude > maxLng) maxLng = p.Longitude;
                if (p.Latitude < minLat) minLat = p.Latitude;
                if (p.Latitude > maxLat) maxLat = p.Latitude;
            }
            return new NdviBbox(minLng, minLat, maxLng, maxLat);
        }

        // Mantém a proporção do bbox, com o lado maior travado em MaxDimension (mín. 1px).
        public static (int width, int height) ResolveDimensions(NdviBbox bbox)
        {
            var spanLng = Math.Abs(bbox.MaxLng - bbox.MinLng);
            var spanLat = Math.Abs(bbox.MaxLat - bbox.MinLat);
            if (spanLng <= 0 || spanLat <= 0) return (MaxDimension, MaxDimension);

            if (spanLng >= spanLat)
            {
                var h = (int)Math.Round(MaxDimension * spanLat / spanLng);
                return (MaxDimension, Math.Max(1, h));
            }
            var w = (int)Math.Round(MaxDimension * spanLng / spanLat);
            return (Math.Max(1, w), MaxDimension);
        }

        public static string BuildRequestBody(
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry, DateTime from, DateTime to, int width, int height)
            => BuildBody(geometry, from, to, width, height, "image/png", Evalscript);

        /// <summary>Corpo do request de zonas — TIFF + o evalscript de índice de classe.</summary>
        public static string BuildZonesRequestBody(
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry, DateTime from, DateTime to, int width, int height)
            => BuildBody(geometry, from, to, width, height, "image/tiff", ZonesEvalscript);

        private static string BuildBody(
            GeoJsonPolygon<GeoJson2DGeographicCoordinates> geometry, DateTime from, DateTime to,
            int width, int height, string formatType, string evalscript)
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
                            type = "sentinel-2-l2a",
                            dataFilter = new
                            {
                                maxCloudCoverage = 80,
                                timeRange = new
                                {
                                    from = from.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                                    to = to.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    }
                },
                output = new
                {
                    width,
                    height,
                    responses = new[]
                    {
                        new { identifier = "default", format = new { type = formatType } }
                    }
                },
                evalscript
            };

            return JsonSerializer.Serialize(payload);
        }

        private static string Truncate(string s) => s.Length <= 300 ? s : s[..300];
    }
}
