using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroWorker.Services;
using StarkAgroWorker.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace StarkAgroWorker.Tests.Services
{
    public class IrrigationAlertSchedulerTests
    {
        // ── ComputeSlope unit tests ───────────────────────────────────────────

        [Fact]
        public void ComputeSlope_LessThanTwoPoints_ReturnsZero()
        {
            var result = IrrigationAlertScheduler.ComputeSlope(new List<(double, double)>
            {
                (1.0, 50.0)
            });
            Assert.Equal(0.0, result);
        }

        [Fact]
        public void ComputeSlope_EmptyList_ReturnsZero()
        {
            var result = IrrigationAlertScheduler.ComputeSlope(new List<(double, double)>());
            Assert.Equal(0.0, result);
        }

        [Fact]
        public void ComputeSlope_DecreasingLine_ReturnsNegativeSlope()
        {
            // 60 at hour 0, 50 at hour 1, 40 at hour 2 → slope = -10/hour
            var points = new List<(double, double)> { (0, 60), (1, 50), (2, 40) };
            var slope = IrrigationAlertScheduler.ComputeSlope(points);
            Assert.Equal(-10.0, slope, precision: 5);
        }

        [Fact]
        public void ComputeSlope_IncreasingLine_ReturnsPositiveSlope()
        {
            var points = new List<(double, double)> { (0, 30), (1, 35), (2, 40) };
            var slope = IrrigationAlertScheduler.ComputeSlope(points);
            Assert.Equal(5.0, slope, precision: 5);
        }

        [Fact]
        public void ComputeSlope_FlatLine_ReturnsZero()
        {
            var points = new List<(double, double)> { (0, 50), (1, 50), (2, 50) };
            var slope = IrrigationAlertScheduler.ComputeSlope(points);
            Assert.Equal(0.0, slope, precision: 5);
        }

        // ── EvaluatePivotAsync integration tests ─────────────────────────────

        private static (Mock<agpDBContext>, Mock<IMongoCollection<Sensor>>, Mock<IMongoCollection<ReadSensor>>, Mock<IMongoCollection<IrrigationAlert>>) BuildDbMocks()
        {
            var db = new Mock<agpDBContext>();
            var sensors = new Mock<IMongoCollection<Sensor>>();
            var reads = new Mock<IMongoCollection<ReadSensor>>();
            var alerts = new Mock<IMongoCollection<IrrigationAlert>>();

            db.Setup(d => d.Sensors).Returns(sensors.Object);
            db.Setup(d => d.ReadSensors).Returns(reads.Object);
            db.Setup(d => d.IrrigationAlerts).Returns(alerts.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            return (db, sensors, reads, alerts);
        }

        private static Mock<IPushNotificationService> BuildPushMock()
        {
            var push = new Mock<IPushNotificationService>();
            push.Setup(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return push;
        }

        private static IrrigationAlertScheduler BuildScheduler()
        {
            var sp = new Mock<IServiceProvider>();
            var logger = new Mock<ILogger<IrrigationAlertScheduler>>();
            return new IrrigationAlertScheduler(sp.Object, logger.Object);
        }

        [Fact]
        public async Task EvaluatePivot_NoSensors_SkipsWithoutInsert()
        {
            var (db, sensors, reads, alerts) = BuildDbMocks();
            var emailService = new Mock<IAlertEmailService>();

            MongoMockHelper.SetupFindList(sensors, new List<Sensor>());

            var pivot = new Pivot { Id = 1, UserId = 10, LimiteInferior = 30m };
            var scheduler = BuildScheduler();

            await scheduler.EvaluatePivotAsync(pivot, db.Object, emailService.Object, BuildPushMock().Object, CancellationToken.None);

            alerts.Verify(a => a.InsertOneAsync(It.IsAny<IrrigationAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EvaluatePivot_NoReadings_SkipsWithoutInsert()
        {
            var (db, sensors, reads, alerts) = BuildDbMocks();
            var emailService = new Mock<IAlertEmailService>();

            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 5, PivoId = 1 } });
            MongoMockHelper.SetupFindList(reads, new List<ReadSensor>());

            var pivot = new Pivot { Id = 1, UserId = 10, LimiteInferior = 30m };
            var scheduler = BuildScheduler();

            await scheduler.EvaluatePivotAsync(pivot, db.Object, emailService.Object, BuildPushMock().Object, CancellationToken.None);

            alerts.Verify(a => a.InsertOneAsync(It.IsAny<IrrigationAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EvaluatePivot_ProjectedAboveLimit_NoAlert()
        {
            var (db, sensors, reads, alerts) = BuildDbMocks();
            var emailService = new Mock<IAlertEmailService>();

            var now = DateTime.UtcNow;

            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 5, PivoId = 1 } });

            // Readings showing humidity increasing: 40 → 50 → 60
            MongoMockHelper.SetupFindList(reads, new List<ReadSensor>
            {
                new() { SensorId = 5, Humidity =40m, Date = now.AddHours(-4), IsAnomaly = false },
                new() { SensorId = 5, Humidity =50m, Date = now.AddHours(-2), IsAnomaly = false },
                new() { SensorId = 5, Humidity =60m, Date = now.AddHours(-1), IsAnomaly = false },
            });

            // LimiteInferior = 30; projected ≈ 60 + positive_slope * 4 >> 30 — no alert
            var pivot = new Pivot { Id = 1, UserId = 10, LimiteInferior = 30m };
            var scheduler = BuildScheduler();

            await scheduler.EvaluatePivotAsync(pivot, db.Object, emailService.Object, BuildPushMock().Object, CancellationToken.None);

            alerts.Verify(a => a.InsertOneAsync(It.IsAny<IrrigationAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EvaluatePivot_ProjectedBelowLimit_InsertsAlert()
        {
            var (db, sensors, reads, alerts) = BuildDbMocks();
            var emailService = new Mock<IAlertEmailService>();

            var now = DateTime.UtcNow;

            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 5, PivoId = 1 } });

            // Humidity declining from 50 → 40 → 32 over 5 hours; projected 4h further < 30
            MongoMockHelper.SetupFindList(reads, new List<ReadSensor>
            {
                new() { SensorId = 5, Humidity =50m, Date = now.AddHours(-5), IsAnomaly = false },
                new() { SensorId = 5, Humidity =40m, Date = now.AddHours(-3), IsAnomaly = false },
                new() { SensorId = 5, Humidity =32m, Date = now.AddHours(-0.5), IsAnomaly = false },
            });

            // No recent dedup alert
            MongoMockHelper.SetupFind(alerts, (IrrigationAlert?)null);

            alerts.Setup(a => a.InsertOneAsync(It.IsAny<IrrigationAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

            var pivot = new Pivot { Id = 1, UserId = 10, LimiteInferior = 30m };
            var scheduler = BuildScheduler();

            await scheduler.EvaluatePivotAsync(pivot, db.Object, emailService.Object, BuildPushMock().Object, CancellationToken.None);

            alerts.Verify(a => a.InsertOneAsync(
                It.Is<IrrigationAlert>(al => al.PivotId == 1 && al.UserId == 10 && al.AlertType == "humidity_low_projected"),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);

            emailService.Verify(e => e.SendIrrigationAlertAsync(
                1, 10, It.IsAny<string?>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), 30m,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EvaluatePivot_DuplicateAlertExists_NoNewInsert()
        {
            var (db, sensors, reads, alerts) = BuildDbMocks();
            var emailService = new Mock<IAlertEmailService>();

            var now = DateTime.UtcNow;

            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 5, PivoId = 1 } });

            MongoMockHelper.SetupFindList(reads, new List<ReadSensor>
            {
                new() { SensorId = 5, Humidity =50m, Date = now.AddHours(-5), IsAnomaly = false },
                new() { SensorId = 5, Humidity =32m, Date = now.AddHours(-0.5), IsAnomaly = false },
            });

            // Return an existing recent alert (dedup should suppress)
            MongoMockHelper.SetupFind(alerts, new IrrigationAlert
            {
                PivotId = 1,
                AlertType = "humidity_low_projected",
                Date = now.AddHours(-1)
            });

            var pivot = new Pivot { Id = 1, UserId = 10, LimiteInferior = 30m };
            var scheduler = BuildScheduler();

            await scheduler.EvaluatePivotAsync(pivot, db.Object, emailService.Object, BuildPushMock().Object, CancellationToken.None);

            alerts.Verify(a => a.InsertOneAsync(It.IsAny<IrrigationAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            emailService.Verify(e => e.SendIrrigationAlertAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EvaluatePivot_EmailFails_AlertStillSaved()
        {
            var (db, sensors, reads, alerts) = BuildDbMocks();
            var emailService = new Mock<IAlertEmailService>();

            var now = DateTime.UtcNow;

            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 5, PivoId = 1 } });

            MongoMockHelper.SetupFindList(reads, new List<ReadSensor>
            {
                new() { SensorId = 5, Humidity =50m, Date = now.AddHours(-5), IsAnomaly = false },
                new() { SensorId = 5, Humidity =32m, Date = now.AddHours(-0.5), IsAnomaly = false },
            });

            MongoMockHelper.SetupFind(alerts, (IrrigationAlert?)null);

            alerts.Setup(a => a.InsertOneAsync(It.IsAny<IrrigationAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

            emailService.Setup(e => e.SendIrrigationAlertAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                    It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SMTP error"));

            var pivot = new Pivot { Id = 1, UserId = 10, LimiteInferior = 30m };
            var scheduler = BuildScheduler();

            // Must not throw
            var ex = await Record.ExceptionAsync(() =>
                scheduler.EvaluatePivotAsync(pivot, db.Object, emailService.Object, BuildPushMock().Object, CancellationToken.None));

            Assert.Null(ex);

            alerts.Verify(a => a.InsertOneAsync(It.IsAny<IrrigationAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
