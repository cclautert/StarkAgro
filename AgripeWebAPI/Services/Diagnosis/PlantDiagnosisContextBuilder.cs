using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MongoDB.Driver;

namespace AgripeWebAPI.Services.Diagnosis
{
    public interface IPlantDiagnosisContextBuilder
    {
        Task<PlantDiagnosisContextSnapshot> BuildAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Monta o retrato da lavoura no momento do laudo.
    /// <para>
    /// O tenant vem de <c>diagnosis.UserId</c> — <b>não</b> de <c>ICurrentUserContext</c>, porque
    /// isto roda no worker, onde não há usuário autenticado.
    /// </para>
    /// </summary>
    public class PlantDiagnosisContextBuilder : IPlantDiagnosisContextBuilder
    {
        private const int MaxSensors = 10;
        private static readonly TimeSpan Window = TimeSpan.FromDays(7);

        private readonly agpDBContext _dbContext;
        private readonly IWeatherForecastService _forecastService;
        private readonly ILogger<PlantDiagnosisContextBuilder> _logger;

        public PlantDiagnosisContextBuilder(
            agpDBContext dbContext,
            IWeatherForecastService forecastService,
            ILogger<PlantDiagnosisContextBuilder> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PlantDiagnosisContextSnapshot> BuildAsync(
            PlantDiagnosis diagnosis,
            CancellationToken cancellationToken)
        {
            var snapshot = new PlantDiagnosisContextSnapshot { CapturedAt = DateTime.UtcNow };

            var user = await _dbContext.Users
                .Find(u => u.Id == diagnosis.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            Pivot? pivot = null;
            if (diagnosis.PivotId.HasValue)
            {
                pivot = await _dbContext.Pivots
                    .Find(p => p.Id == diagnosis.PivotId.Value && p.UserId == diagnosis.UserId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            // Sem pivô não há sensores para correlacionar: o laudo sai só com a foto,
            // e o prompt é instruído a dizer isso em vez de inventar contexto.
            if (pivot is null) return snapshot;

            snapshot.PivotName = pivot.Name;
            snapshot.LimiteInferior = pivot.LimiteInferior ?? user?.LimiteInferior;
            snapshot.LimiteSuperior = pivot.LimiteSuperior ?? user?.LimiteSuperior;

            var since = DateTime.UtcNow - Window;

            var sensors = await _dbContext.Sensors
                .Find(s => s.PivoId == pivot.Id && s.UserId == diagnosis.UserId)
                .Limit(MaxSensors)
                .ToListAsync(cancellationToken);

            var humidities = new List<decimal>();
            var dailyAverages = new Dictionary<DateTime, List<decimal>>();

            foreach (var sensor in sensors)
            {
                var readings = await _dbContext.ReadSensors
                    .Find(r => r.SensorId == sensor.Id && r.Date >= since && r.IsAnomaly != true)
                    .SortByDescending(r => r.Date)
                    .ToListAsync(cancellationToken);

                if (readings.Count == 0) continue;

                var latest = readings[0];
                snapshot.LastReadings.Add(new SensorReadingSnapshot
                {
                    SensorCode = sensor.Code,
                    Quadrante = sensor.Quadrante,
                    Humidity = latest.Humidity,
                    Temperature = latest.Temperature,
                    Date = latest.Date
                });

                foreach (var reading in readings.Where(r => r.Humidity.HasValue))
                {
                    humidities.Add(reading.Humidity!.Value);

                    var day = reading.Date.Date;
                    if (!dailyAverages.TryGetValue(day, out var bucket))
                    {
                        bucket = [];
                        dailyAverages[day] = bucket;
                    }
                    bucket.Add(reading.Humidity!.Value);
                }
            }

            if (humidities.Count > 0)
            {
                snapshot.MoistureAvg7d = Math.Round(humidities.Average(), 1);
            }

            // Quantos dias, na última semana, a média diária ficou acima do limite superior.
            // É este número que sustenta a frase que vende o produto: "solo encharcado por N dias,
            // condição favorável ao patógeno" — e por isso não pode ser maior que a janela.
            //
            // A janela em horas (now - 7d) toca 8 datas de calendário; sem este recorte o laudo
            // chega a dizer "8 dias acima do limite" numa análise de 7 dias.
            if (snapshot.LimiteSuperior.HasValue)
            {
                var oldestDay = DateTime.UtcNow.Date.AddDays(-6);

                snapshot.DaysAboveUpperLimit = dailyAverages
                    .Where(day => day.Key >= oldestDay)
                    .Count(day => day.Value.Average() > snapshot.LimiteSuperior.Value);
            }

            var sensorIds = sensors.Select(s => s.Id).ToList();
            if (sensorIds.Count > 0)
            {
                var anomalyFilter = Builders<SensorAnomaly>.Filter.And(
                    Builders<SensorAnomaly>.Filter.In(a => a.SensorId, sensorIds),
                    Builders<SensorAnomaly>.Filter.Eq(a => a.UserId, diagnosis.UserId),
                    Builders<SensorAnomaly>.Filter.Eq(a => a.Acknowledged, false));

                snapshot.OpenAnomalies = (await _dbContext.SensorAnomalies
                    .Find(anomalyFilter)
                    .ToListAsync(cancellationToken)).Count;
            }

            snapshot.IrrigationAlerts7d = (await _dbContext.IrrigationAlerts
                .Find(a => a.PivotId == pivot.Id && a.Date >= since)
                .ToListAsync(cancellationToken)).Count;

            if (pivot.Latitude.HasValue && pivot.Longitude.HasValue)
            {
                try
                {
                    var forecast = await _forecastService.GetForecastAsync(
                        pivot.Latitude.Value, pivot.Longitude.Value, 7, cancellationToken);

                    if (forecast.IsAvailable)
                    {
                        snapshot.ForecastSummary = BuildForecastSummary(forecast);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Clima é enriquecimento: sem ele o laudo ainda sai, só sem a previsão.
                    _logger.LogWarning(ex, "Could not fetch forecast for diagnosis {Id}", diagnosis.Id);
                }
            }

            return snapshot;
        }

        private static string BuildForecastSummary(WeatherForecast forecast)
        {
            var days = forecast.DailyForecasts
                .Take(5)
                .Select(d => $"{d.Date:dd/MM}: {d.PrecipitationMm:0.0} mm");

            return $"Precipitação total prevista: {forecast.TotalPrecipitationMm:0.0} mm " +
                   $"(fonte: {forecast.Source}). Por dia — {string.Join(" | ", days)}";
        }
    }
}
