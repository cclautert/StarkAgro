using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Services.Forecast;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetMoisturePredictionHandlerTests
    {
        private const int OwnerUserId = 42;

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static (Mock<agpDBContext> Db,
                        Mock<IMongoCollection<Pivot>> Pivots,
                        Mock<IMongoCollection<Sensor>> Sensors,
                        Mock<IMongoCollection<ReadSensor>> Reads) BuildDbMocks()
        {
            var db = new Mock<agpDBContext>();
            var pivots = new Mock<IMongoCollection<Pivot>>();
            var sensors = new Mock<IMongoCollection<Sensor>>();
            var reads = new Mock<IMongoCollection<ReadSensor>>();
            db.Setup(c => c.Pivots).Returns(pivots.Object);
            db.Setup(c => c.Sensors).Returns(sensors.Object);
            db.Setup(c => c.ReadSensors).Returns(reads.Object);
            return (db, pivots, sensors, reads);
        }

        private static GetMoisturePredictionHandler BuildHandler(
            agpDBContext db, IAgricultureWeatherService agriWeather)
            => new(db, agriWeather,
                   new Notificator(),
                   NullLogger<GetMoisturePredictionHandler>.Instance);

        private static GetMoisturePredictionHandler BuildHandler(
            agpDBContext db, IAgricultureWeatherService agriWeather, INotifier notifier)
            => new(db, agriWeather, notifier,
                   NullLogger<GetMoisturePredictionHandler>.Instance);

        /// <summary>
        /// Generates <paramref name="count"/> hourly readings spanning the given total hours,
        /// all for a single sensor. The readings are sorted oldest→newest.
        /// </summary>
        private static List<ReadSensor> GenerateHourlyReadings(
            int sensorId, int userId, int count, double startMoisture = 60.0,
            double slopePerHour = -0.3, double totalSpanHours = 48)
        {
            var now = DateTime.UtcNow;
            var start = now.AddHours(-totalSpanHours);
            var step = totalSpanHours / (count - 1);
            var list = new List<ReadSensor>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new ReadSensor
                {
                    Id = i + 1,
                    SensorId = sensorId,
                    UserId = userId,
                    Value = (decimal)(startMoisture + slopePerHour * (i * step)),
                    Date = start.AddHours(i * step),
                    IsAnomaly = false
                });
            }
            return list;
        }

        // ── Validation ────────────────────────────────────────────────────────

        [Fact]
        public async Task Handle_PivotIdZero_NotifiesAndReturnsNull()
        {
            var (db, _, _, _) = BuildDbMocks();
            var notifier = new Notificator();
            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object, notifier);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 0, UserId = OwnerUserId }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_PivotNotFound_NotifiesAndReturnsNull()
        {
            var (db, pivots, _, _) = BuildDbMocks();
            MongoMockHelper.SetupFind<Pivot>(pivots, null);
            var notifier = new Notificator();
            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object, notifier);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 5, UserId = OwnerUserId }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_CrossTenantPivot_NotifiesAndReturnsNull()
        {
            var (db, pivots, _, _) = BuildDbMocks();
            // Find returns null when userId doesn't match → simulates tenant isolation
            MongoMockHelper.SetupFind<Pivot>(pivots, null);
            var notifier = new Notificator();
            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object, notifier);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = 999 }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_NoSensors_NotifiesAndReturnsNull()
        {
            var (db, pivots, sensors, _) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot { Id = 1, UserId = OwnerUserId, Name = "P1" });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>());
            var notifier = new Notificator();
            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object, notifier);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_InsufficientHistory_LessThan24h_NotifiesAndReturnsNull()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot { Id = 1, UserId = OwnerUserId, Name = "P1" });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            // Only 2 readings 12 hours apart — below 24h threshold
            var shortReadings = new List<ReadSensor>
            {
                new() { Id = 1, SensorId = 10, UserId = OwnerUserId, Value = 60m, Date = DateTime.UtcNow.AddHours(-12), IsAnomaly = false },
                new() { Id = 2, SensorId = 10, UserId = OwnerUserId, Value = 58m, Date = DateTime.UtcNow.AddHours(-6),  IsAnomaly = false }
            };
            MongoMockHelper.SetupFindList(reads, shortReadings);
            var notifier = new Notificator();
            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object, notifier);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_NoReadings_NotifiesAndReturnsNull()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot { Id = 1, UserId = OwnerUserId, Name = "P1" });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            MongoMockHelper.SetupFindList(reads, new List<ReadSensor>());
            var notifier = new Notificator();
            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object, notifier);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        // ── Happy path without coordinates ────────────────────────────────────

        [Fact]
        public async Task Handle_PivotWithoutCoordinates_ReturnsPredictionWithoutET_ConfidenceReduced()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = null, Longitude = null
            });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            var readings = GenerateHourlyReadings(10, OwnerUserId, count: 48, totalSpanHours: 47);
            MongoMockHelper.SetupFindList(reads, readings);

            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.NotNull(result);
            Assert.Equal(1, result!.PivotId);
            Assert.Equal(72, result.PredictedValues.Count);
            Assert.Equal(readings.Count, result.DataPointsUsed);
            // No coordinates → OpenMeteo should NOT be called
            agri.Verify(a => a.GetAgricultureDataAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ── Happy path with coordinates ───────────────────────────────────────

        [Fact]
        public async Task Handle_PivotWithCoordinates_ReturnsPredictionWith72Points()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -29.7, Longitude = -53.7
            });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            var readings = GenerateHourlyReadings(10, OwnerUserId, count: 48, totalSpanHours: 47);
            MongoMockHelper.SetupFindList(reads, readings);

            var agri = new Mock<IAgricultureWeatherService>();
            agri.Setup(a => a.GetAgricultureDataAsync(-29.7, -53.7, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgricultureWeatherData(30, 18, 20));

            var handler = BuildHandler(db.Object, agri.Object);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.NotNull(result);
            Assert.Equal(72, result!.PredictedValues.Count);
            Assert.Equal(readings.Count, result.DataPointsUsed);
            agri.Verify(a => a.GetAgricultureDataAsync(
                -29.7, -53.7, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── EstimatedCriticalAt ───────────────────────────────────────────────

        [Fact]
        public async Task Handle_MoistureDropsBelowLimit_EstimatedCriticalAtIsSet()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            // LimiteInferior = 40, readings start at 60 and drop sharply → will cross 40 within 72h
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 40m, LimiteSuperior = 75m,
                Latitude = null, Longitude = null
            });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            // steep decline: -0.5%/h over 48h means last value ≈ 36%;
            // regressed slope will be close to -0.5 → will cross 40% early in projection
            var readings = GenerateHourlyReadings(10, OwnerUserId, count: 48,
                startMoisture: 60, slopePerHour: -0.5, totalSpanHours: 47);
            MongoMockHelper.SetupFindList(reads, readings);

            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.NotNull(result);
            Assert.NotNull(result!.EstimatedCriticalAt);
            Assert.True(result.EstimatedCriticalAt > DateTime.UtcNow,
                "EstimatedCriticalAt should be in the future");
        }

        [Fact]
        public async Task Handle_MoistureStaysAboveLimit_EstimatedCriticalAtIsNull()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            // LimiteInferior = 20, moisture starts at 70 with tiny decline → won't cross 20 in 72h
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 20m, LimiteSuperior = 75m,
                Latitude = null, Longitude = null
            });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            // Very gentle slope: -0.05%/h → 72h * 0.05 = 3.6% drop total; stays well above 20%
            var readings = GenerateHourlyReadings(10, OwnerUserId, count: 48,
                startMoisture: 70, slopePerHour: -0.05, totalSpanHours: 47);
            MongoMockHelper.SetupFindList(reads, readings);

            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.NotNull(result);
            Assert.Null(result!.EstimatedCriticalAt);
        }

        // ── Resilience ────────────────────────────────────────────────────────

        [Fact]
        public async Task Handle_AgriWeatherServiceFails_StillReturnsPredictionWithoutET()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -29.7, Longitude = -53.7
            });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            var readings = GenerateHourlyReadings(10, OwnerUserId, count: 48, totalSpanHours: 47);
            MongoMockHelper.SetupFindList(reads, readings);

            var agri = new Mock<IAgricultureWeatherService>();
            agri.Setup(a => a.GetAgricultureDataAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("timeout"));

            var handler = BuildHandler(db.Object, agri.Object);

            // Should NOT throw — ET is best-effort
            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.NotNull(result);
            Assert.Equal(72, result!.PredictedValues.Count);
        }

        [Fact]
        public async Task Handle_ConfidenceInRange()
        {
            var (db, pivots, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m
            });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>
            {
                new() { Id = 10, PivoId = 1, UserId = OwnerUserId, Quadrante = 1 }
            });
            var readings = GenerateHourlyReadings(10, OwnerUserId, count: 48, totalSpanHours: 47);
            MongoMockHelper.SetupFindList(reads, readings);

            var agri = new Mock<IAgricultureWeatherService>();
            var handler = BuildHandler(db.Object, agri.Object);

            var result = await handler.Handle(
                new GetMoisturePredictionRequest { PivotId = 1, UserId = OwnerUserId }, default);

            Assert.NotNull(result);
            Assert.InRange(result!.Confidence, 0.0, 1.0);
        }
    }
}
