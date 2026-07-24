using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Gera o GeoTIFF de doses (prescrição para taxa variável) de uma passagem, sob demanda.
    /// Espelho de <see cref="INdviZoneService"/>, mas <b>sem cache</b>: a dose depende do
    /// <see cref="FertilizationProfile"/>, que o admin pode editar — um raster cacheado viraria uma
    /// prescrição errada e silenciosa no campo. Regenera sempre (gate <c>Sentinel2Enabled</c>; paga PU
    /// da Process API por download, aceitável por ser esporádico). <c>null</c> em qualquer falha.
    /// </summary>
    public interface INdviPrescriptionRasterService
    {
        Task<byte[]?> GetTiffAsync(
            MonitoredArea area, NdviReading reading, FertilizationProfile profile, string? nutrient,
            CancellationToken cancellationToken);
    }

    public class NdviPrescriptionRasterService : INdviPrescriptionRasterService
    {
        private readonly agpDBContext _dbContext;
        private readonly ICdseTokenProvider _tokenProvider;
        private readonly ICdseProcessService _processService;
        private readonly ILogger<NdviPrescriptionRasterService> _logger;

        public NdviPrescriptionRasterService(
            agpDBContext dbContext,
            ICdseTokenProvider tokenProvider,
            ICdseProcessService processService,
            ILogger<NdviPrescriptionRasterService> logger)
        {
            _dbContext = dbContext;
            _tokenProvider = tokenProvider;
            _processService = processService;
            _logger = logger;
        }

        public async Task<byte[]?> GetTiffAsync(
            MonitoredArea area, NdviReading reading, FertilizationProfile profile, string? nutrient,
            CancellationToken cancellationToken)
        {
            var settings = await _dbContext.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.Sentinel2Enabled
                || string.IsNullOrWhiteSpace(settings.CdseClientId)
                || string.IsNullOrWhiteSpace(settings.CdseClientSecret))
            {
                _logger.LogWarning("Prescrição: NDVI desligado ou sem credenciais CDSE — geração pulada (área {AreaId}).", area.Id);
                return null;
            }

            var token = await _tokenProvider.GetTokenAsync(settings.CdseClientId!, settings.CdseClientSecret!, cancellationToken);
            if (string.IsNullOrEmpty(token)) return null;

            var evalscript = DoseEvalscript.Build(profile, nutrient);

            // Mesma janela do bucket da passagem, sem truncar a hora (a lição do overlay transparente).
            var from = reading.AcquisitionDate;
            var tiff = await _processService.GetPrescriptionTiffAsync(
                token, area.Geometry, from, from.AddDays(1), evalscript, cancellationToken);
            if (tiff is null || tiff.Length == 0)
            {
                _logger.LogWarning("Prescrição: TIFF vazio para a área {AreaId} (passagem {Date:O}).", area.Id, reading.AcquisitionDate);
                return null;
            }

            _logger.LogInformation("Prescrição: GeoTIFF gerado — área {AreaId}, reading {ReadingId}, perfil {ProfileId}, {Bytes} bytes.",
                area.Id, reading.Id, profile.Id, tiff.Length);

            return tiff;
        }
    }
}
