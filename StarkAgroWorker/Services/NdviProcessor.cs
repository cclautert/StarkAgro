using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;
using MongoDB.Driver;

namespace StarkAgroWorker.Services
{
    /// <summary>
    /// Agenda e busca o NDVI das áreas monitoradas. Clona a mecânica de fila do
    /// <see cref="PlantDiagnosisProcessor"/> (claim atômico, retentativa, zumbis), adaptada ao
    /// agendamento por <c>NextFetchAt</c> (~revisita do Sentinel-2). O tenant vem do documento da área.
    /// </summary>
    public sealed class NdviProcessor : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan ZombieTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RefetchCadence = TimeSpan.FromDays(5);
        private const int MaxRetries = 3;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NdviProcessor> _logger;
        private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

        public NdviProcessor(IServiceProvider serviceProvider, ILogger<NdviProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await RunAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "NdviProcessor tick failed"); }
            }
        }

        /// <summary>Um tick. Público para teste sem subir o BackgroundService.</summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<agpDBContext>();

            // Kill-switch: desligado, o worker não reivindica nada (não gera custo de PU).
            var settings = await db.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.Sentinel2Enabled) return;

            var fetchService = scope.ServiceProvider.GetRequiredService<INdviFetchService>();

            await ReleaseZombiesAsync(db, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var area = await ClaimNextAsync(db, cancellationToken);
                if (area is null) break;

                try
                {
                    var outcome = await fetchService.FetchAsync(area, cancellationToken);
                    switch (outcome.Status)
                    {
                        case NdviFetchStatus.Success:
                            await CompleteAsync(db, area, outcome.MaxAcquisitionDate, cancellationToken);
                            break;
                        case NdviFetchStatus.Disabled:
                            await SnoozeAsync(db, area, cancellationToken);
                            break;
                        default:
                            await FailAsync(db, area, outcome.Reason ?? "Falha ao buscar NDVI.", cancellationToken);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NdviProcessor: failed to fetch NDVI for area {Id}", area.Id);
                    await FailAsync(db, area, ex.Message, cancellationToken);
                }
            }
        }

        private async Task<MonitoredArea?> ClaimNextAsync(agpDBContext db, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var filter = Builders<MonitoredArea>.Filter.And(
                Builders<MonitoredArea>.Filter.Eq(a => a.MonitoringEnabled, true),
                Builders<MonitoredArea>.Filter.Eq(a => a.Status, MonitoredAreaStatus.Idle),
                Builders<MonitoredArea>.Filter.Or(
                    Builders<MonitoredArea>.Filter.Eq(a => a.NextFetchAt, (DateTime?)null),
                    Builders<MonitoredArea>.Filter.Lte(a => a.NextFetchAt, now)),
                Builders<MonitoredArea>.Filter.Or(
                    Builders<MonitoredArea>.Filter.Eq(a => a.NextAttemptAt, (DateTime?)null),
                    Builders<MonitoredArea>.Filter.Lte(a => a.NextAttemptAt, now)));

            var update = Builders<MonitoredArea>.Update
                .Set(a => a.Status, MonitoredAreaStatus.Fetching)
                .Set(a => a.ProcessingStartedAt, now)
                .Set(a => a.WorkerId, _workerId)
                .Set(a => a.UpdatedAt, now);

            return await db.MonitoredAreas.FindOneAndUpdateAsync(
                filter, update,
                new FindOneAndUpdateOptions<MonitoredArea> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }

        private static async Task CompleteAsync(agpDBContext db, MonitoredArea area, string? maxDate, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            await db.MonitoredAreas.UpdateOneAsync(
                a => a.Id == area.Id,
                Builders<MonitoredArea>.Update
                    .Set(a => a.Status, MonitoredAreaStatus.Idle)
                    .Set(a => a.LastFetchAt, now)
                    .Set(a => a.NextFetchAt, now + RefetchCadence)
                    .Set(a => a.NextAttemptAt, (DateTime?)null)
                    .Set(a => a.RetryCount, 0)
                    .Set(a => a.FailureReason, (string?)null)
                    .Set(a => a.LastAcquisitionDate, maxDate)
                    .Set(a => a.UpdatedAt, now),
                null, cancellationToken);
        }

        /// <summary>Kill-switch pegou entre o claim e o fetch: solta a área e re-checa em 1 h.</summary>
        private static async Task SnoozeAsync(agpDBContext db, MonitoredArea area, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            await db.MonitoredAreas.UpdateOneAsync(
                a => a.Id == area.Id,
                Builders<MonitoredArea>.Update
                    .Set(a => a.Status, MonitoredAreaStatus.Idle)
                    .Set(a => a.NextFetchAt, now.AddHours(1))
                    .Set(a => a.UpdatedAt, now),
                null, cancellationToken);
        }

        private async Task ReleaseZombiesAsync(agpDBContext db, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow - ZombieTimeout;
            var stuck = await db.MonitoredAreas
                .Find(a => a.Status == MonitoredAreaStatus.Fetching && a.ProcessingStartedAt < cutoff)
                .ToListAsync(cancellationToken);

            foreach (var area in stuck)
            {
                _logger.LogWarning("NdviProcessor: releasing stuck area {Id}", area.Id);
                await FailAsync(db, area, "Busca de NDVI interrompida.", cancellationToken);
            }
        }

        private static async Task FailAsync(agpDBContext db, MonitoredArea area, string reason, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var retry = area.RetryCount + 1;
            var giveUp = retry >= MaxRetries;
            var backoff = retry switch
            {
                1 => TimeSpan.FromMinutes(1),
                2 => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromMinutes(15)
            };
            var status = giveUp ? MonitoredAreaStatus.Failed : MonitoredAreaStatus.Idle;

            await db.MonitoredAreas.UpdateOneAsync(
                a => a.Id == area.Id,
                Builders<MonitoredArea>.Update
                    .Set(a => a.Status, status)
                    .Set(a => a.RetryCount, retry)
                    .Set(a => a.NextAttemptAt, now + backoff)
                    .Set(a => a.FailureReason, reason)
                    .Set(a => a.UpdatedAt, now),
                null, cancellationToken);
        }
    }
}
