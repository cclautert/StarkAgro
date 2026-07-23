using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Gera (sob demanda) e cacheia o PNG de overlay NDVI de uma passagem — espelho de
    /// <see cref="INdviZoneService"/>, mas para a imagem colorida do mapa. Existe porque o fetch
    /// só gera overlay para a passagem <b>mais nova de cada ciclo</b>: ao navegar o histórico para
    /// uma passagem antiga (a maioria não tem PNG), o mapa ficava só com o contorno. Aqui, abrir
    /// uma passagem de céu limpo rende o mapa na hora e o cacheia em <c>OverlayImageFileId</c> —
    /// paga PU da Process API só na 1ª vez por reading; aberturas seguintes vêm do GridFS.
    /// <para>Passagem nublada não tem pixel válido → sem imagem (retorna null, o front mostra o aviso).</para>
    /// </summary>
    public interface INdviOverlayImageService
    {
        Task<byte[]?> GetOrCreateOverlayAsync(MonitoredArea area, NdviReading reading, CancellationToken cancellationToken);
    }

    public class NdviOverlayImageService : INdviOverlayImageService
    {
        private readonly agpDBContext _dbContext;
        private readonly ICdseTokenProvider _tokenProvider;
        private readonly ICdseProcessService _processService;
        private readonly INdviOverlayStore _overlayStore;
        private readonly ILogger<NdviOverlayImageService> _logger;

        public NdviOverlayImageService(
            agpDBContext dbContext,
            ICdseTokenProvider tokenProvider,
            ICdseProcessService processService,
            INdviOverlayStore overlayStore,
            ILogger<NdviOverlayImageService> logger)
        {
            _dbContext = dbContext;
            _tokenProvider = tokenProvider;
            _processService = processService;
            _overlayStore = overlayStore;
            _logger = logger;
        }

        public async Task<byte[]?> GetOrCreateOverlayAsync(
            MonitoredArea area, NdviReading reading, CancellationToken cancellationToken)
        {
            // Cache: já gerado → serve do bucket, sem tocar na CDSE.
            if (reading.OverlayImageFileId is not null)
                return await _overlayStore.DownloadAsync(reading.OverlayImageFileId.Value, cancellationToken);

            // Nublada não rende imagem: não há pixel válido para colorir. Não é erro — é honesto.
            if (reading.CloudRejected) return null;

            var settings = await _dbContext.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.Sentinel2Enabled
                || string.IsNullOrWhiteSpace(settings.CdseClientId)
                || string.IsNullOrWhiteSpace(settings.CdseClientSecret))
            {
                _logger.LogWarning("Overlay: NDVI desligado ou sem credenciais CDSE — geração pulada (área {AreaId}).", area.Id);
                return null;
            }

            var token = await _tokenProvider.GetTokenAsync(settings.CdseClientId!, settings.CdseClientSecret!, cancellationToken);
            if (string.IsNullOrEmpty(token)) return null;

            // Mesma janela do bucket da passagem, sem truncar a hora (a lição do overlay transparente:
            // os buckets da Statistical API são de 1 dia a partir do timeRange.from, não da meia-noite).
            var from = reading.AcquisitionDate;
            var png = await _processService.GetNdviOverlayPngAsync(
                token, area.Geometry, from, from.AddDays(1), cancellationToken);
            if (png is null || png.Length == 0)
            {
                _logger.LogWarning("Overlay: PNG vazio para a área {AreaId} (passagem {Date:O}).", area.Id, reading.AcquisitionDate);
                return null;
            }

            var fileId = await _overlayStore.UploadAsync(
                png, $"ndvi-{area.Id}-{reading.Id}.png", "image/png", cancellationToken);

            await _dbContext.NdviReadings.UpdateOneAsync(
                r => r.Id == reading.Id,
                Builders<NdviReading>.Update.Set(r => r.OverlayImageFileId, fileId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Overlay: PNG gerado e cacheado — área {AreaId}, reading {ReadingId}, {Bytes} bytes.",
                area.Id, reading.Id, png.Length);

            return png;
        }
    }
}
