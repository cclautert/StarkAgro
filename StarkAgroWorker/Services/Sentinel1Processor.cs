using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Sentinel1;
using MongoDB.Driver;

namespace StarkAgroWorker.Services
{
    /// <summary>
    /// Busca a série de radar (Sentinel-1) das áreas monitoradas. <b>Itera + dedup</b> (padrão do
    /// <c>FireWatchProcessor</c>/<c>ClimateWatchProcessor</c>), NÃO o claim atômico do
    /// <c>NdviProcessor</c> — o claim usa os campos de worker do <c>MonitoredArea</c>, que são do
    /// NDVI; um 2º processor clamando os mesmos campos brigaria. O <c>Sentinel1FetchService</c> pula
    /// a chamada à CDSE quando não há passagem nova, então iterar não custa PU à toa.
    /// </summary>
    public sealed class Sentinel1Processor : BackgroundService
    {
        // Tick largo: o S1 tem ~6 dias de revisita, o FetchService pula quando a última é recente.
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<Sentinel1Processor> _logger;

        public Sentinel1Processor(IServiceProvider serviceProvider, ILogger<Sentinel1Processor> logger)
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
                catch (Exception ex) { _logger.LogError(ex, "Sentinel1Processor tick failed"); }
            }
        }

        /// <summary>Um tick. Público para teste sem subir o BackgroundService.</summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<agpDBContext>();

            var settings = await db.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            // Kill-switch: desligado, o worker não reivindica nem chama a CDSE.
            if (settings is null || !settings.Sentinel1Enabled) return;

            var fetch = scope.ServiceProvider.GetRequiredService<ISentinel1FetchService>();

            var areas = await db.MonitoredAreas.Find(a => a.MonitoringEnabled).ToListAsync(cancellationToken);
            _logger.LogInformation("Sentinel1Processor: {Count} área(s) monitorada(s)", areas.Count);

            foreach (var area in areas)
            {
                try
                {
                    var outcome = await fetch.FetchAsync(area, cancellationToken);
                    if (outcome.Status == Sentinel1FetchStatus.Success && outcome.NewReadings > 0)
                        _logger.LogInformation("S1: área {AreaId} — {Count} passagem(ns) nova(s)", area.Id, outcome.NewReadings);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sentinel1Processor: erro na área {AreaId}", area.Id);
                }
            }
        }
    }
}
