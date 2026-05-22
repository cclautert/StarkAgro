using AgripeWebAPI.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetIrrigationTrendHandlerTests
    {
        private const int OwnerUserId = 42;

        private static GetIrrigationTrendRequest CreateRequest(int pivotId = 1, int? userId = null)
            => new() { PivotId = pivotId, UserId = userId ?? OwnerUserId, NumberOfReads = 10 };

        private static (Mock<agpDBContext> Db, Mock<IMongoCollection<Pivot>> Pivots,
                       Mock<IMongoCollection<User>> Users, Mock<IMongoCollection<Sensor>> Sensors,
                       Mock<IMongoCollection<ReadSensor>> Reads) BuildDbMocks()
        {
            var db = new Mock<agpDBContext>();
            var pivots = new Mock<IMongoCollection<Pivot>>();
            var users = new Mock<IMongoCollection<User>>();
            var sensors = new Mock<IMongoCollection<Sensor>>();
            var reads = new Mock<IMongoCollection<ReadSensor>>();
            db.Setup(c => c.Pivots).Returns(pivots.Object);
            db.Setup(c => c.Users).Returns(users.Object);
            db.Setup(c => c.Sensors).Returns(sensors.Object);
            db.Setup(c => c.ReadSensors).Returns(reads.Object);
            return (db, pivots, users, sensors, reads);
        }

        private static GetIrrigationTrendHandler BuildHandler(
            agpDBContext db,
            IWeatherForecastService forecast,
            INotifier notifier,
            WeatherForecastSettings? settings = null)
            => new(
                db,
                forecast,
                notifier,
                Options.Create(settings ?? new WeatherForecastSettings()),
                NullLogger<GetIrrigationTrendHandler>.Instance);

        [Fact]
        public async Task Handle_PivotIdMissing_NotifiesAndReturnsNull()
        {
            var notifier = new Notificator();
            var (db, _, _, _, _) = BuildDbMocks();
            var forecast = new Mock<IWeatherForecastService>();
            var handler = BuildHandler(db.Object, forecast.Object, notifier);

            var result = await handler.Handle(new GetIrrigationTrendRequest { UserId = OwnerUserId }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            forecast.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Handle_PivotNotFound_NotifiesAndReturnsNull()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind<Pivot>(pivots, null);
            var forecast = new Mock<IWeatherForecastService>();
            var handler = BuildHandler(db.Object, forecast.Object, notifier);

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            forecast.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Handle_PivotOwnedByDifferentUser_NotifiesAndReturnsNull()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            // Pivot lookup filters by UserId; simulate "not found" for caller
            MongoMockHelper.SetupFind<Pivot>(pivots, null);
            var forecast = new Mock<IWeatherForecastService>();
            var handler = BuildHandler(db.Object, forecast.Object, notifier);

            var result = await handler.Handle(CreateRequest(userId: 999), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            forecast.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Handle_NoSensors_ReturnsResponseWithoutForecast()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot { Id = 1, UserId = OwnerUserId, Name = "P1", LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor>());

            var forecast = new Mock<IWeatherForecastService>();
            var handler = BuildHandler(db.Object, forecast.Object, notifier);

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result!.PivotId);
            Assert.False(result.NeedsIrrigation);
            Assert.False(result.IrrigationPostponed);
            Assert.Null(result.WeatherForecast);
            forecast.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Handle_NeedsIrrigationWithoutCoordinates_DoesNotCallForecast()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = null, Longitude = null
            });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 10, UserId = OwnerUserId, PivoId = 1, Quadrante = 1 } });
            MongoMockHelper.SetupFind(reads, new ReadSensor { Id = 100, SensorId = 10, Value = 18m, Date = DateTime.UtcNow });

            var forecast = new Mock<IWeatherForecastService>();
            var handler = BuildHandler(db.Object, forecast.Object, notifier);

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.NeedsIrrigation);
            Assert.False(result.IrrigationPostponed);
            Assert.Null(result.WeatherForecast);
            forecast.Verify(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_NeedsIrrigationAndForecastAboveThreshold_PostponesIrrigation()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -27.5, Longitude = -48.5
            });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 10, UserId = OwnerUserId, PivoId = 1, Quadrante = 1 } });
            MongoMockHelper.SetupFind(reads, new ReadSensor { Id = 100, SensorId = 10, Value = 20m, Date = DateTime.UtcNow });

            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(-27.5, -48.5, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WeatherForecast
                {
                    IsAvailable = true,
                    Source = "OpenMeteo",
                    TotalPrecipitationMm = 8.4,
                    DailyForecasts = Array.Empty<DailyForecast>()
                });

            var handler = BuildHandler(db.Object, forecast.Object, notifier,
                new WeatherForecastSettings { ForecastHorizonDays = 5, RainThresholdMm = 5.0 });

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.NeedsIrrigation);
            Assert.True(result.IrrigationPostponed);
            Assert.Contains("8.4", result.PostponeReason);
            Assert.Contains("OpenMeteo", result.PostponeReason);
            Assert.NotNull(result.WeatherForecast);
            Assert.Equal(8.4, result.WeatherForecast!.TotalPrecipitationMm);
        }

        [Fact]
        public async Task Handle_NeedsIrrigationAndForecastBelowThreshold_KeepsRecommendation()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -27.5, Longitude = -48.5
            });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 10, UserId = OwnerUserId, PivoId = 1, Quadrante = 1 } });
            MongoMockHelper.SetupFind(reads, new ReadSensor { Id = 100, SensorId = 10, Value = 20m, Date = DateTime.UtcNow });

            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WeatherForecast
                {
                    IsAvailable = true,
                    Source = "OpenMeteo",
                    TotalPrecipitationMm = 1.2,
                    DailyForecasts = Array.Empty<DailyForecast>()
                });

            var handler = BuildHandler(db.Object, forecast.Object, notifier,
                new WeatherForecastSettings { ForecastHorizonDays = 5, RainThresholdMm = 5.0 });

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.NeedsIrrigation);
            Assert.False(result.IrrigationPostponed);
            Assert.Null(result.PostponeReason);
            Assert.NotNull(result.WeatherForecast);
        }

        [Fact]
        public async Task Handle_PivotRainThresholdOverridesUserAndSettings_PostponesWhenPivotThresholdExceeded()
        {
            // pivot.RainThresholdMm = 3.0 → 2.5 mm forecast should NOT postpone with settings=5.0 but SHOULD when threshold=2.0
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -27.5, Longitude = -48.5,
                RainThresholdMm = 2.0
            });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m, RainThresholdMm = 8.0 });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 10, UserId = OwnerUserId, PivoId = 1, Quadrante = 1 } });
            MongoMockHelper.SetupFind(reads, new ReadSensor { Id = 100, SensorId = 10, Value = 18m, Date = DateTime.UtcNow });

            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WeatherForecast
                {
                    IsAvailable = true, Source = "OpenMeteo",
                    TotalPrecipitationMm = 2.5, DailyForecasts = Array.Empty<DailyForecast>()
                });

            var handler = BuildHandler(db.Object, forecast.Object, notifier,
                new WeatherForecastSettings { ForecastHorizonDays = 5, RainThresholdMm = 5.0 });

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.IrrigationPostponed);
        }

        [Fact]
        public async Task Handle_UserRainThresholdUsed_WhenPivotThresholdIsNull()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -27.5, Longitude = -48.5,
                RainThresholdMm = null
            });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m, RainThresholdMm = 2.0 });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 10, UserId = OwnerUserId, PivoId = 1, Quadrante = 1 } });
            MongoMockHelper.SetupFind(reads, new ReadSensor { Id = 100, SensorId = 10, Value = 18m, Date = DateTime.UtcNow });

            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WeatherForecast
                {
                    IsAvailable = true, Source = "OpenMeteo",
                    TotalPrecipitationMm = 2.5, DailyForecasts = Array.Empty<DailyForecast>()
                });

            var handler = BuildHandler(db.Object, forecast.Object, notifier,
                new WeatherForecastSettings { ForecastHorizonDays = 5, RainThresholdMm = 5.0 });

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.IrrigationPostponed);
        }

        [Fact]
        public async Task Handle_SettingsThresholdUsed_WhenPivotAndUserThresholdAreNull()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -27.5, Longitude = -48.5,
                RainThresholdMm = null
            });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m, RainThresholdMm = null });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 10, UserId = OwnerUserId, PivoId = 1, Quadrante = 1 } });
            MongoMockHelper.SetupFind(reads, new ReadSensor { Id = 100, SensorId = 10, Value = 18m, Date = DateTime.UtcNow });

            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WeatherForecast
                {
                    IsAvailable = true, Source = "OpenMeteo",
                    TotalPrecipitationMm = 4.0, DailyForecasts = Array.Empty<DailyForecast>()
                });

            // 4.0 mm < 5.0 mm (settings default) → should NOT postpone
            var handler = BuildHandler(db.Object, forecast.Object, notifier,
                new WeatherForecastSettings { ForecastHorizonDays = 5, RainThresholdMm = 5.0 });

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result!.IrrigationPostponed);
        }

        [Fact]
        public async Task Handle_ForecastUnavailable_KeepsRecommendation()
        {
            var notifier = new Notificator();
            var (db, pivots, users, sensors, reads) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P1",
                LimiteInferior = 25m, LimiteSuperior = 75m,
                Latitude = -27.5, Longitude = -48.5
            });
            MongoMockHelper.SetupFind(users, new User { Id = OwnerUserId, LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFindList(sensors, new List<Sensor> { new() { Id = 10, UserId = OwnerUserId, PivoId = 1, Quadrante = 1 } });
            MongoMockHelper.SetupFind(reads, new ReadSensor { Id = 100, SensorId = 10, Value = 18m, Date = DateTime.UtcNow });

            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WeatherForecast.Unavailable("OpenMeteo"));

            var handler = BuildHandler(db.Object, forecast.Object, notifier);

            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.NeedsIrrigation);
            Assert.False(result.IrrigationPostponed);
            Assert.Null(result.PostponeReason);
            Assert.NotNull(result.WeatherForecast);
            Assert.False(result.WeatherForecast!.IsAvailable);
        }
    }
}
