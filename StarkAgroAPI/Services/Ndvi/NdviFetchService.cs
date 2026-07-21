using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Globalization;

namespace StarkAgroAPI.Services.Ndvi
{
    public enum NdviFetchStatus { Success, Failed, Disabled }

    /// <param name="MaxAcquisitionDate">Data (yyyy-MM-dd) da passagem mais nova gravada, para avançar a área.</param>
    public record NdviFetchOutcome(NdviFetchStatus Status, string? Reason = null, string? MaxAcquisitionDate = null);

    /// <summary>
    /// Busca o NDVI de uma área na CDSE e grava as passagens novas. <b>Serviço puro injetado</b>
    /// (não handler MediatR) — o assembly scan exporia como request handler, e o tenant vem do
    /// documento da área, nunca de contexto de usuário (mesma razão de <c>IPlantDiagnosisProcessingService</c>).
    /// </summary>
    public interface INdviFetchService
    {
        Task<NdviFetchOutcome> FetchAsync(MonitoredArea area, CancellationToken cancellationToken);
    }

    public class NdviFetchService : INdviFetchService
    {
        private readonly agpDBContext _dbContext;
        private readonly ICdseTokenProvider _tokenProvider;
        private readonly ICdseStatisticalService _statisticalService;
        private readonly ICdseProcessService _processService;
        private readonly INdviOverlayStore _overlayStore;
        private readonly ILogger<NdviFetchService> _logger;

        public NdviFetchService(
            agpDBContext dbContext,
            ICdseTokenProvider tokenProvider,
            ICdseStatisticalService statisticalService,
            ICdseProcessService processService,
            INdviOverlayStore overlayStore,
            ILogger<NdviFetchService> logger)
        {
            _dbContext = dbContext;
            _tokenProvider = tokenProvider;
            _statisticalService = statisticalService;
            _processService = processService;
            _overlayStore = overlayStore;
            _logger = logger;
        }

        public async Task<NdviFetchOutcome> FetchAsync(MonitoredArea area, CancellationToken cancellationToken)
        {
            var settings = await _dbContext.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.Sentinel2Enabled
                || string.IsNullOrWhiteSpace(settings.CdseClientId)
                || string.IsNullOrWhiteSpace(settings.CdseClientSecret))
            {
                return new NdviFetchOutcome(NdviFetchStatus.Disabled, "NDVI desligado ou sem credenciais CDSE.");
            }

            var token = await _tokenProvider.GetTokenAsync(settings.CdseClientId!, settings.CdseClientSecret!, cancellationToken);
            if (string.IsNullOrEmpty(token))
                return new NdviFetchOutcome(NdviFetchStatus.Failed, "Falha ao obter token da CDSE.");

            var to = DateTime.UtcNow;
            var from = ResolveFrom(area.LastAcquisitionDate, to);

            var stats = await _statisticalService.GetStatisticsAsync(token, area.Geometry, from, to, cancellationToken);
            if (stats is null)
                return new NdviFetchOutcome(NdviFetchStatus.Failed, "Falha na Statistical API da CDSE.");

            var now = DateTime.UtcNow;
            var maxDate = area.LastAcquisitionDate;
            NdviReading? overlayCandidate = null;

            foreach (var s in stats.OrderBy(x => x.AcquisitionDate))
            {
                var dateKey = s.AcquisitionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                // Dedup por data: só passagens mais novas que a última já vista entram.
                if (area.LastAcquisitionDate is not null
                    && string.CompareOrdinal(dateKey, area.LastAcquisitionDate) <= 0)
                {
                    continue;
                }

                var cloudRejected = s.ValidSampleCount == 0;
                var reading = new NdviReading
                {
                    Id = await _dbContext.GetNextIdAsync(nameof(NdviReading), cancellationToken),
                    AreaId = area.Id,
                    UserId = area.UserId,
                    AcquisitionDate = s.AcquisitionDate,
                    NdviMean = s.Mean,
                    NdviMin = s.Min,
                    NdviMax = s.Max,
                    NdviStdev = s.Stdev,
                    CloudCoveragePct = s.CloudPct,
                    CloudRejected = cloudRejected,
                    NdviCostCents = settings.NdviCostCents,
                    CreatedAt = now
                };

                try
                {
                    await _dbContext.NdviReadings.InsertOneAsync(reading, null, cancellationToken);

                    // Overlay só para a passagem mais nova com pixel válido (nublada não rende imagem).
                    if (!cloudRejected) overlayCandidate = reading;
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Índice único {AreaId, AcquisitionDate}: outra corrida já gravou esta passagem — no-op.
                    _logger.LogDebug("NDVI: passagem {Date} da área {AreaId} já existia (dedup).", dateKey, area.Id);
                }

                if (maxDate is null || string.CompareOrdinal(dateKey, maxDate) > 0) maxDate = dateKey;
            }

            // Overlay é acessório: renderiza o PNG da passagem mais nova e o anexa. Qualquer falha
            // aqui é engolida — a série de tendência (dado primário) não pode quebrar por causa da imagem.
            if (overlayCandidate is not null)
                await TryAttachOverlayAsync(area, overlayCandidate, token, cancellationToken);

            return new NdviFetchOutcome(NdviFetchStatus.Success, MaxAcquisitionDate: maxDate);
        }

        private async Task TryAttachOverlayAsync(
            MonitoredArea area, NdviReading reading, string token, CancellationToken cancellationToken)
        {
            try
            {
                // Janela de 1 dia em torno da passagem — o mosaico do PNG usa aquela aquisição.
                var day = reading.AcquisitionDate.Date;
                var png = await _processService.GetNdviOverlayPngAsync(
                    token, area.Geometry, day, day.AddDays(1), cancellationToken);
                if (png is null || png.Length == 0)
                {
                    _logger.LogWarning("NDVI overlay: PNG vazio para a área {AreaId} (passagem {Date}).",
                        area.Id, reading.AcquisitionDate);
                    return;
                }

                var fileId = await _overlayStore.UploadAsync(
                    png, $"ndvi-{area.Id}-{reading.Id}.png", "image/png", cancellationToken);

                await _dbContext.NdviReadings.UpdateOneAsync(
                    r => r.Id == reading.Id,
                    Builders<NdviReading>.Update.Set(r => r.OverlayImageFileId, fileId),
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "NDVI overlay: falha ao gerar/gravar o PNG da área {AreaId}.", area.Id);
            }
        }

        private static DateTime ResolveFrom(string? lastAcquisitionDate, DateTime to)
        {
            if (lastAcquisitionDate is not null
                && DateTime.TryParse(lastAcquisitionDate, CultureInfo.InvariantCulture,
                       DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var last))
            {
                var from = last.AddDays(1);
                return from < to ? from : to.AddDays(-7);
            }
            return to.AddDays(-30); // primeira busca: janela de 30 dias
        }
    }
}
