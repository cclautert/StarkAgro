using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Climate;
using MongoDB.Driver;

namespace StarkAgroWorker.Services
{
    /// <summary>
    /// Alerta de risco de geada (mín ≤ limiar) e calor extremo (máx ≥ limiar) nos próximos dias,
    /// por área. Scheduler PRÓPRIO (não um tick do fogo): kill-switch independente, e uma falha não
    /// derruba a outra vigilância. Serviço puro — tenant vem do documento da área. <b>Custo zero</b>
    /// (Open-Meteo é gratuito). Usa <see cref="IAgricultureWeatherService"/>, que traz temperatura —
    /// o <c>IWeatherForecastService</c> só tem precipitação.
    /// </summary>
    public sealed class ClimateWatchProcessor : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private const int ForecastDays = 3;
        private const int DefaultHeatC = 35; // fallback quando o doc legado desserializa 0 (senão "máx ≥ 0" dispara sempre)

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ClimateWatchProcessor> _logger;

        public ClimateWatchProcessor(IServiceProvider serviceProvider, ILogger<ClimateWatchProcessor> logger)
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
                catch (Exception ex) { _logger.LogError(ex, "ClimateWatchProcessor tick failed"); }
            }
        }

        /// <summary>Um tick. Público para teste sem subir o BackgroundService.</summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<agpDBContext>();

            var settings = await db.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            // Kill-switch: desligado, o worker NÃO busca previsão.
            if (settings is null || !settings.ClimateAlertsEnabled) return;

            var weather = scope.ServiceProvider.GetRequiredService<IAgricultureWeatherService>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var frostC = settings.FrostAlertTempC;
            // Doc legado desserializa HeatAlertTempC=0 → "máx ≥ 0" dispararia sempre; cai para 35.
            var heatC = settings.HeatAlertTempC > 0 ? settings.HeatAlertTempC : DefaultHeatC;

            var areas = await db.MonitoredAreas.Find(a => a.MonitoringEnabled).ToListAsync(cancellationToken);
            _logger.LogInformation("ClimateWatchProcessor: {Count} área(s) monitorada(s)", areas.Count);

            foreach (var area in areas)
            {
                try
                {
                    await EvaluateAreaAsync(area, frostC, heatC, db, weather, push, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ClimateWatchProcessor: erro na área {AreaId}", area.Id);
                }
            }
        }

        private async Task EvaluateAreaAsync(
            MonitoredArea area, int frostC, int heatC, agpDBContext db,
            IAgricultureWeatherService weather, IPushNotificationService push, CancellationToken cancellationToken)
        {
            var centroid = AreaCentroid.Of(area);
            if (centroid is null) return; // sem geometria utilizável — pulada, não estoura

            var forecast = await weather.GetAgricultureDataAsync(
                centroid.Value.lat, centroid.Value.lng, ForecastDays, cancellationToken);
            if (forecast is null || forecast.Count == 0) return; // previsão indisponível — sem alerta falso

            var now = DateTime.UtcNow;
            var newAlerts = new List<ClimateAlert>();

            foreach (var day in forecast)
            {
                var forecastDate = day.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

                if (day.TempMin <= frostC)
                    await TryInsertAsync(db, area, ClimateAlertType.Frost, forecastDate, day.TempMin, frostC, now, newAlerts, cancellationToken);

                if (day.TempMax >= heatC)
                    await TryInsertAsync(db, area, ClimateAlertType.Heat, forecastDate, day.TempMax, heatC, now, newAlerts, cancellationToken);
            }

            if (newAlerts.Count == 0) return;

            // Um push por área por tick, nomeando o risco mais próximo. Falha de envio não perde os
            // alertas já gravados.
            var soonest = newAlerts.OrderBy(a => a.ForecastDate).First();
            var name = string.IsNullOrWhiteSpace(area.Name) ? $"Área {area.Id}" : area.Name;
            var (title, body) = soonest.AlertType == ClimateAlertType.Frost
                ? ("❄️ Risco de geada", $"{name}: mínima de {soonest.TemperatureC:0.#} °C prevista para {soonest.ForecastDate:dd/MM}.")
                : ("🌡️ Calor extremo", $"{name}: máxima de {soonest.TemperatureC:0.#} °C prevista para {soonest.ForecastDate:dd/MM}.");

            try
            {
                await push.SendAsync(area.UserId, title, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClimateWatch: push falhou para a área {AreaId} (alertas já gravados)", area.Id);
            }
        }

        private async Task TryInsertAsync(
            agpDBContext db, MonitoredArea area, string alertType, DateTime forecastDate,
            double temperatureC, int thresholdC, DateTime now, List<ClimateAlert> newAlerts, CancellationToken cancellationToken)
        {
            var alert = new ClimateAlert
            {
                Id = await db.GetNextIdAsync(nameof(ClimateAlert), cancellationToken),
                AreaId = area.Id,
                UserId = area.UserId,
                AlertType = alertType,
                ForecastDate = forecastDate,
                TemperatureC = Math.Round(temperatureC, 1),
                ThresholdC = thresholdC,
                CreatedAt = now
            };

            try
            {
                await db.ClimateAlerts.InsertOneAsync(alert, null, cancellationToken);
                newAlerts.Add(alert);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Índice único {AreaId, AlertType, ForecastDate}: mesmo risco já alertado — no-op.
            }
        }
    }
}
