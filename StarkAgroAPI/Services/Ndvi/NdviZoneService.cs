using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Gera (sob demanda) e cacheia o GeoTIFF de zonas de uma passagem. On-demand + cache: paga PU
    /// da Process API só na 1ª vez por reading; downloads seguintes vêm do GridFS. O worker de
    /// fetch nunca chama isto — é acionado pelo endpoint de download.
    /// </summary>
    public interface INdviZoneService
    {
        Task<byte[]?> GetOrCreateTiffAsync(MonitoredArea area, NdviReading reading, CancellationToken cancellationToken);
    }

    public class NdviZoneService : INdviZoneService
    {
        private readonly agpDBContext _dbContext;
        private readonly ICdseTokenProvider _tokenProvider;
        private readonly ICdseProcessService _processService;
        private readonly INdviOverlayStore _overlayStore;
        private readonly ILogger<NdviZoneService> _logger;

        public NdviZoneService(
            agpDBContext dbContext,
            ICdseTokenProvider tokenProvider,
            ICdseProcessService processService,
            INdviOverlayStore overlayStore,
            ILogger<NdviZoneService> logger)
        {
            _dbContext = dbContext;
            _tokenProvider = tokenProvider;
            _processService = processService;
            _overlayStore = overlayStore;
            _logger = logger;
        }

        public async Task<byte[]?> GetOrCreateTiffAsync(
            MonitoredArea area, NdviReading reading, CancellationToken cancellationToken)
        {
            // Cache: já gerado → serve do bucket, sem tocar na CDSE.
            if (reading.ZoneImageFileId is not null)
                return await _overlayStore.DownloadAsync(reading.ZoneImageFileId.Value, cancellationToken);

            var settings = await _dbContext.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.Sentinel2Enabled
                || string.IsNullOrWhiteSpace(settings.CdseClientId)
                || string.IsNullOrWhiteSpace(settings.CdseClientSecret))
            {
                _logger.LogWarning("Zonas: NDVI desligado ou sem credenciais CDSE — geração pulada (área {AreaId}).", area.Id);
                return null;
            }

            var token = await _tokenProvider.GetTokenAsync(settings.CdseClientId!, settings.CdseClientSecret!, cancellationToken);
            if (string.IsNullOrEmpty(token)) return null;

            // Mesma janela do bucket da passagem, sem truncar a hora (a lição do overlay transparente).
            var from = reading.AcquisitionDate;
            var tiff = await _processService.GetNdviZonesTiffAsync(
                token, area.Geometry, from, from.AddDays(1), cancellationToken);
            if (tiff is null || tiff.Length == 0)
            {
                _logger.LogWarning("Zonas: TIFF vazio para a área {AreaId} (passagem {Date:O}).", area.Id, reading.AcquisitionDate);
                return null;
            }

            var fileId = await _overlayStore.UploadAsync(
                tiff, $"zonas-{area.Id}-{reading.Id}.tiff", "image/tiff", cancellationToken);

            await _dbContext.NdviReadings.UpdateOneAsync(
                r => r.Id == reading.Id,
                Builders<NdviReading>.Update.Set(r => r.ZoneImageFileId, fileId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Zonas: GeoTIFF gerado e cacheado — área {AreaId}, reading {ReadingId}, {Bytes} bytes.",
                area.Id, reading.Id, tiff.Length);

            return tiff;
        }
    }
}
