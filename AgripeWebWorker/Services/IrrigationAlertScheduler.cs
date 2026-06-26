using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MongoDB.Driver;

namespace AgripeWebWorker.Services
{
    public sealed class IrrigationAlertScheduler : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TrendWindow = TimeSpan.FromHours(6);
        private static readonly TimeSpan ProjectionHorizon = TimeSpan.FromHours(4);
        private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromHours(2);
        private const string AlertType = "humidity_low_projected";

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<IrrigationAlertScheduler> _logger;

        public IrrigationAlertScheduler(
            IServiceProvider serviceProvider,
            ILogger<IrrigationAlertScheduler> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IrrigationAlertScheduler tick failed");
                }
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<agpDBContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IAlertEmailService>();
            var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var pivots = await db.Pivots
                .Find(p => p.LimiteInferior != null)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("IrrigationAlertScheduler: evaluating {Count} pivot(s)", pivots.Count);

            foreach (var pivot in pivots)
            {
                try
                {
                    await EvaluatePivotAsync(pivot, db, emailService, pushService, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IrrigationAlertScheduler: error evaluating pivot {PivotId}", pivot.Id);
                }
            }
        }

        public async Task EvaluatePivotAsync(
            Pivot pivot,
            agpDBContext db,
            IAlertEmailService emailService,
            IPushNotificationService pushService,
            CancellationToken cancellationToken)
        {
            var limiteInferior = pivot.LimiteInferior!.Value;
            var now = DateTime.UtcNow;
            var windowStart = now - TrendWindow;

            var sensors = await db.Sensors
                .Find(s => s.PivoId == pivot.Id)
                .ToListAsync(cancellationToken);

            if (sensors.Count == 0)
            {
                _logger.LogDebug("IrrigationAlertScheduler: pivot {PivotId} has no sensors, skipping", pivot.Id);
                return;
            }

            var sensorIds = sensors.Select(s => s.Id).ToList();

            var filter = Builders<ReadSensor>.Filter.And(
                Builders<ReadSensor>.Filter.In(r => r.SensorId, sensorIds),
                Builders<ReadSensor>.Filter.Gte(r => r.Date, windowStart),
                Builders<ReadSensor>.Filter.Ne(r => r.IsAnomaly, true));

            var readings = await db.ReadSensors
                .Find(filter)
                .SortBy(r => r.Date)
                .ToListAsync(cancellationToken);

            if (readings.Count == 0)
            {
                _logger.LogDebug("IrrigationAlertScheduler: pivot {PivotId} has no readings in the last 6h", pivot.Id);
                return;
            }

            // Current average: latest non-anomaly reading per sensor
            var latestPerSensor = readings
                .GroupBy(r => r.SensorId)
                .Select(g => (double)(g.OrderByDescending(r => r.Date).First().Humidity ?? 0))
                .ToList();

            var currentAverage = latestPerSensor.Average();

            // Linear regression over all 6h readings to compute slope (units/hour)
            var points = readings
                .Select(r => ((r.Date - windowStart).TotalHours, (double)(r.Humidity ?? 0)))
                .ToList();

            var slope = ComputeSlope(points);
            var projected = currentAverage + slope * ProjectionHorizon.TotalHours;

            _logger.LogDebug(
                "IrrigationAlertScheduler: pivot {PivotId} avg={Avg:F2} slope={Slope:F4}/h projected={Proj:F2} limite={Limite}",
                pivot.Id, currentAverage, slope, projected, limiteInferior);

            if ((decimal)projected >= limiteInferior)
                return;

            // Deduplication: suppress if an alert of the same type was fired in the last 2h
            var dedupCutoff = now - DeduplicationWindow;
            var recent = await db.IrrigationAlerts
                .Find(a => a.PivotId == pivot.Id && a.AlertType == AlertType && a.Date >= dedupCutoff)
                .FirstOrDefaultAsync(cancellationToken);

            if (recent is not null)
            {
                _logger.LogDebug(
                    "IrrigationAlertScheduler: pivot {PivotId} dedup — alert already fired at {Date:O}",
                    pivot.Id, recent.Date);
                return;
            }

            var alert = new IrrigationAlert
            {
                Id = await db.GetNextIdAsync("irrigation_alerts", cancellationToken),
                PivotId = pivot.Id,
                UserId = pivot.UserId,
                AlertType = AlertType,
                CurrentAverage = Math.Round((decimal)currentAverage, 2),
                ProjectedValue = Math.Round((decimal)projected, 2),
                LimiteInferior = limiteInferior,
                SlopePerHour = Math.Round(slope, 6),
                Date = now
            };

            await db.IrrigationAlerts.InsertOneAsync(alert, null, cancellationToken);

            _logger.LogInformation(
                "IrrigationAlert fired: pivot {PivotId} user {UserId} projected={Proj:F2} < limite={Limite}",
                pivot.Id, pivot.UserId, projected, limiteInferior);

            try
            {
                await emailService.SendIrrigationAlertAsync(
                    pivot.Id,
                    pivot.UserId,
                    pivot.Name,
                    alert.CurrentAverage,
                    alert.ProjectedValue,
                    limiteInferior,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "IrrigationAlertScheduler: email send failed for pivot {PivotId}; alert was still saved",
                    pivot.Id);
            }

            try
            {
                var pivotName = pivot.Name ?? pivot.Id.ToString();
                await pushService.SendAsync(
                    pivot.UserId,
                    "Alerta de Irrigação",
                    $"Pivô {pivotName}: umidade projetada {alert.ProjectedValue:F1}% < limite {alert.LimiteInferior:F1}%",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "IrrigationAlertScheduler: push notification failed for pivot {PivotId}",
                    pivot.Id);
            }
        }

        /// <summary>
        /// Least-squares linear regression slope. Returns units per hour.
        /// </summary>
        public static double ComputeSlope(List<(double hours, double value)> points)
        {
            if (points.Count < 2) return 0.0;

            var n = points.Count;
            var sumX = points.Sum(p => p.hours);
            var sumY = points.Sum(p => p.value);
            var sumXY = points.Sum(p => p.hours * p.value);
            var sumXX = points.Sum(p => p.hours * p.hours);

            var denom = n * sumXX - sumX * sumX;
            if (Math.Abs(denom) < 1e-10) return 0.0;

            return (n * sumXY - sumX * sumY) / denom;
        }
    }
}
