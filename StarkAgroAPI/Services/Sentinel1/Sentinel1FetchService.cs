using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Sentinel1
{
    public enum Sentinel1FetchStatus { Success, Failed, Disabled, Skipped }

    public record Sentinel1FetchOutcome(Sentinel1FetchStatus Status, int NewReadings = 0, string? Reason = null);

    /// <summary>
    /// Busca a série de radar (Sentinel-1) de uma área e grava as passagens novas. <b>Serviço puro</b>
    /// (não handler), tenant vem do documento da área — como <c>NdviFetchService</c>. Órbita FIXA
    /// (<c>DESCENDING</c>): a série nunca mistura geometria de visada.
    /// </summary>
    public interface ISentinel1FetchService
    {
        Task<Sentinel1FetchOutcome> FetchAsync(MonitoredArea area, CancellationToken cancellationToken);
    }

    public class Sentinel1FetchService : ISentinel1FetchService
    {
        /// <summary>Órbita fixa da série — trocar isto recomeça a série numa geometria de visada nova.</summary>
        public const string OrbitDirection = "DESCENDING";

        // O worker itera (sem claim). Para não pagar Statistical (custa PU mesmo sem passagem nova)
        // a cada tick, só busca se a última leitura for mais velha que isto — perto do ciclo do S1.
        private static readonly TimeSpan MinRefetchGap = TimeSpan.FromDays(5);

        private readonly agpDBContext _dbContext;
        private readonly ICdseTokenProvider _tokens;
        private readonly ICdseSentinel1Service _s1Service;
        private readonly ILogger<Sentinel1FetchService> _logger;

        public Sentinel1FetchService(
            agpDBContext dbContext,
            ICdseTokenProvider tokens,
            ICdseSentinel1Service s1Service,
            ILogger<Sentinel1FetchService> logger)
        {
            _dbContext = dbContext;
            _tokens = tokens;
            _s1Service = s1Service;
            _logger = logger;
        }

        public async Task<Sentinel1FetchOutcome> FetchAsync(MonitoredArea area, CancellationToken cancellationToken)
        {
            var settings = await _dbContext.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.Sentinel1Enabled
                || string.IsNullOrWhiteSpace(settings.CdseClientId)
                || string.IsNullOrWhiteSpace(settings.CdseClientSecret))
            {
                return new Sentinel1FetchOutcome(Sentinel1FetchStatus.Disabled, Reason: "S1 desligado ou sem credenciais CDSE.");
            }

            var to = DateTime.UtcNow;

            // Última passagem já gravada (bounded aos últimos 40 dias). Se recente, pula a chamada à
            // CDSE — não há passagem nova para pagar.
            var recent = await _dbContext.Sentinel1Readings
                .Find(r => r.AreaId == area.Id && r.AcquisitionDate >= to.AddDays(-40))
                .ToListAsync(cancellationToken);
            var last = recent.Count > 0 ? recent.Max(r => r.AcquisitionDate) : (DateTime?)null;

            if (last is not null && to - last.Value < MinRefetchGap)
                return new Sentinel1FetchOutcome(Sentinel1FetchStatus.Skipped);

            var token = await _tokens.GetTokenAsync(settings.CdseClientId!, settings.CdseClientSecret!, cancellationToken);
            if (string.IsNullOrEmpty(token))
                return new Sentinel1FetchOutcome(Sentinel1FetchStatus.Failed, Reason: "Falha ao obter token da CDSE.");

            var from = last?.AddDays(1) ?? to.AddDays(-30);
            var stats = await _s1Service.GetStatisticsAsync(token, area.Geometry, from, to, OrbitDirection, cancellationToken);
            if (stats is null)
                return new Sentinel1FetchOutcome(Sentinel1FetchStatus.Failed, Reason: "Falha na Statistical API do S1.");

            var now = DateTime.UtcNow;
            var newCount = 0;

            foreach (var s in stats.OrderBy(x => x.AcquisitionDate))
            {
                // Só passagens mais novas que a última já vista.
                if (last is not null && s.AcquisitionDate <= last.Value) continue;
                if (s.ValidSampleCount == 0) continue; // sem pixel útil — não grava buraco

                var reading = new Sentinel1Reading
                {
                    Id = await _dbContext.GetNextIdAsync(nameof(Sentinel1Reading), cancellationToken),
                    AreaId = area.Id,
                    UserId = area.UserId,
                    AcquisitionDate = s.AcquisitionDate,
                    OrbitDirection = OrbitDirection,
                    RviMean = s.RviMean,
                    VvMean = s.VvMean,
                    VhMean = s.VhMean,
                    Sentinel1CostCents = settings.Sentinel1CostCents,
                    CreatedAt = now
                };

                try
                {
                    await _dbContext.Sentinel1Readings.InsertOneAsync(reading, null, cancellationToken);
                    newCount++;
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Índice único {AreaId, AcquisitionDate, OrbitDirection}: outra corrida gravou — no-op.
                    _logger.LogDebug("S1: passagem {Date:O} da área {AreaId} já existia (dedup).", s.AcquisitionDate, area.Id);
                }
            }

            return new Sentinel1FetchOutcome(Sentinel1FetchStatus.Success, newCount);
        }
    }
}
