using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Fire;
using StarkAgroAPI.Services.Ndvi;
using MongoDB.Driver;

namespace StarkAgroWorker.Services
{
    /// <summary>
    /// Vigia focos de calor (NASA FIRMS) ao redor das áreas monitoradas e dispara alerta/push.
    /// Clona a mecânica do <see cref="IrrigationAlertScheduler"/> (PeriodicTimer, scope por tick,
    /// dedup antes de gravar, envio em try/catch isolado). Serviço puro: o tenant vem do documento
    /// da área (<c>WorkerUserContext.UserId</c> é null), como no NDVI. <b>Custo zero</b> — FIRMS é
    /// gratuito; a flag é freio de ruído/rate-limit, não de dinheiro.
    /// </summary>
    public sealed class FireWatchProcessor : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        // VIIRS 375 m (S-NPP + NOAA-20) — melhor que o 1 km do MODIS para foco em lavoura.
        private static readonly string[] Sources = ["VIIRS_SNPP_NRT", "VIIRS_NOAA20_NRT"];
        private const int DayRange = 1;
        private const int DefaultRadiusKm = 10;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FireWatchProcessor> _logger;

        public FireWatchProcessor(IServiceProvider serviceProvider, ILogger<FireWatchProcessor> logger)
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
                catch (Exception ex) { _logger.LogError(ex, "FireWatchProcessor tick failed"); }
            }
        }

        /// <summary>Um tick. Público para teste sem subir o BackgroundService.</summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<agpDBContext>();

            var settings = await db.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            // Kill-switch: desligado ou sem MAP_KEY, o worker NÃO faz nenhuma chamada externa.
            if (settings is null || !settings.FireAlertsEnabled || string.IsNullOrWhiteSpace(settings.FirmsMapKey))
                return;

            var firms = scope.ServiceProvider.GetRequiredService<IFirmsHotspotService>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            var radiusKm = settings.FireAlertRadiusKm > 0 ? settings.FireAlertRadiusKm : DefaultRadiusKm;

            var areas = await db.MonitoredAreas
                .Find(a => a.MonitoringEnabled)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("FireWatchProcessor: {Count} área(s) monitorada(s)", areas.Count);

            foreach (var area in areas)
            {
                try
                {
                    await ScanAreaAsync(area, settings.FirmsMapKey!, radiusKm, db, firms, push, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FireWatchProcessor: erro na área {AreaId}", area.Id);
                }
            }
        }

        private async Task ScanAreaAsync(
            MonitoredArea area, string mapKey, int radiusKm, agpDBContext db,
            IFirmsHotspotService firms, IPushNotificationService push, CancellationToken cancellationToken)
        {
            if (area.Geometry is null) return;

            var bbox = FireAreaBbox.Expand(CdseProcessService.ComputeBbox(area.Geometry), radiusKm);
            var (centerLat, centerLng) = FireAreaBbox.Center(bbox);
            var now = DateTime.UtcNow;

            // Junta as duas fontes VIIRS. Uma fonte que falha (null) não derruba a outra.
            var hotspots = new List<FireHotspotDto>();
            foreach (var source in Sources)
            {
                var found = await firms.GetHotspotsAsync(mapKey, source, bbox, DayRange, cancellationToken);
                if (found is not null) hotspots.AddRange(found);
            }

            var newCount = 0;
            foreach (var h in hotspots)
            {
                var reading = new FireHotspot
                {
                    Id = await db.GetNextIdAsync(nameof(FireHotspot), cancellationToken),
                    AreaId = area.Id,
                    UserId = area.UserId,
                    Latitude = h.Latitude,
                    Longitude = h.Longitude,
                    AcquiredAt = h.AcquiredAt,
                    Satellite = h.Satellite,
                    Confidence = h.Confidence,
                    Frp = h.Frp,
                    DistanceKm = Math.Round(FireAreaBbox.DistanceKm(centerLat, centerLng, h.Latitude, h.Longitude), 1),
                    CreatedAt = now
                };

                try
                {
                    await db.FireHotspots.InsertOneAsync(reading, null, cancellationToken);
                    newCount++;
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Índice único {AreaId, lat, lng, AcquiredAt, Satellite}: reentrega do FIRMS — no-op.
                }
            }

            if (newCount == 0) return;

            _logger.LogInformation(
                "FireWatch: área {AreaId} user {UserId} — {Count} foco(s) novo(s)", area.Id, area.UserId, newCount);

            // Um push por área por tick (nunca um por foco — um incêndio grande viraria dezenas de
            // notificações). Falha de envio NÃO perde os focos já gravados.
            try
            {
                var name = string.IsNullOrWhiteSpace(area.Name) ? $"Área {area.Id}" : area.Name;
                await push.SendAsync(
                    area.UserId,
                    "🔥 Foco de calor detectado",
                    $"{newCount} foco(s) de calor a até {radiusKm} km de {name} (satélite, latência ~1h).",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FireWatch: push falhou para a área {AreaId} (focos já gravados)", area.Id);
            }
        }
    }
}
