using StarkAgroAPI.Configuration;
using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using StarkAgroAPI.Domain.Handlers.Pivots;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Pivots
{
    public class GetPivotForecastHandlerTests
    {
        private const int OwnerUserId = 42;

        private static (Mock<agpDBContext> Db, Mock<IMongoCollection<Pivot>> Pivots) BuildDbMocks()
        {
            var db = new Mock<agpDBContext>();
            var pivots = new Mock<IMongoCollection<Pivot>>();
            db.Setup(c => c.Pivots).Returns(pivots.Object);
            return (db, pivots);
        }

        private static GetPivotForecastHandler BuildHandler(
            agpDBContext db,
            IWeatherForecastService forecast,
            INotifier notifier,
            WeatherForecastSettings? settings = null)
            => new(db, forecast, notifier, Options.Create(settings ?? new WeatherForecastSettings { PivotDashboardForecastDays = 7 }));

        [Fact]
        public async Task Handle_PivotIdMissing_NotifiesAndReturnsNull()
        {
            var (db, _) = BuildDbMocks();
            var notifier = new Notificator();
            var forecast = new Mock<IWeatherForecastService>();

            var handler = BuildHandler(db.Object, forecast.Object, notifier);
            var result = await handler.Handle(new GetPivotForecastRequest { UserId = OwnerUserId }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            forecast.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        public async Task Handle_DaysOutOfRange_NotifiesAndReturnsNull(int days)
        {
            var (db, _) = BuildDbMocks();
            var notifier = new Notificator();
            var forecast = new Mock<IWeatherForecastService>();

            var handler = BuildHandler(db.Object, forecast.Object, notifier);
            var result = await handler.Handle(
                new GetPivotForecastRequest { PivotId = 1, Days = days, UserId = OwnerUserId },
                default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            forecast.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Handle_PivotNotFound_NotifiesAndReturnsNull()
        {
            var (db, pivots) = BuildDbMocks();
            MongoMockHelper.SetupFind<Pivot>(pivots, null);
            var notifier = new Notificator();
            var forecast = new Mock<IWeatherForecastService>();

            var handler = BuildHandler(db.Object, forecast.Object, notifier);
            var result = await handler.Handle(
                new GetPivotForecastRequest { PivotId = 1, UserId = OwnerUserId },
                default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            forecast.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Handle_PivotWithoutCoordinates_ReturnsHasCoordinatesFalse()
        {
            var (db, pivots) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P",
                Latitude = null, Longitude = null
            });
            var notifier = new Notificator();
            var forecast = new Mock<IWeatherForecastService>();

            var handler = BuildHandler(db.Object, forecast.Object, notifier);
            var result = await handler.Handle(
                new GetPivotForecastRequest { PivotId = 1, UserId = OwnerUserId },
                default);

            Assert.NotNull(result);
            Assert.False(result!.HasCoordinates);
            Assert.Null(result.Forecast);
            Assert.NotNull(result.Message);
            forecast.Verify(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_PivotWithCoordinates_ReturnsForecast()
        {
            var (db, pivots) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P",
                Latitude = -29.7, Longitude = -53.7
            });
            var notifier = new Notificator();
            var forecast = new Mock<IWeatherForecastService>();
            var sample = new WeatherForecast
            {
                IsAvailable = true,
                Source = "OpenMeteo",
                TotalPrecipitationMm = 12.4,
                DailyForecasts = Array.Empty<DailyForecast>()
            };
            forecast
                .Setup(f => f.GetForecastAsync(-29.7, -53.7, 7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sample);

            var handler = BuildHandler(db.Object, forecast.Object, notifier);
            var result = await handler.Handle(
                new GetPivotForecastRequest { PivotId = 1, Days = 7, UserId = OwnerUserId },
                default);

            Assert.NotNull(result);
            Assert.True(result!.HasCoordinates);
            Assert.NotNull(result.Forecast);
            Assert.Equal(12.4, result.Forecast!.TotalPrecipitationMm);
            Assert.Null(result.Message);
            Assert.Equal(7, result.Days);
        }

        [Fact]
        public async Task Handle_DaysFallsBackToSettings_WhenNotProvided()
        {
            var (db, pivots) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P",
                Latitude = 0, Longitude = 0
            });
            var notifier = new Notificator();
            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WeatherForecast { IsAvailable = true, Source = "OpenMeteo" });

            var handler = BuildHandler(db.Object, forecast.Object, notifier,
                new WeatherForecastSettings { PivotDashboardForecastDays = 5 });

            var result = await handler.Handle(
                new GetPivotForecastRequest { PivotId = 1, UserId = OwnerUserId },
                default);

            Assert.NotNull(result);
            Assert.Equal(5, result!.Days);
            forecast.Verify(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), 5, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ForecastUnavailable_PopulatesMessage()
        {
            var (db, pivots) = BuildDbMocks();
            MongoMockHelper.SetupFind(pivots, new Pivot
            {
                Id = 1, UserId = OwnerUserId, Name = "P",
                Latitude = -29.7, Longitude = -53.7
            });
            var notifier = new Notificator();
            var forecast = new Mock<IWeatherForecastService>();
            forecast
                .Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WeatherForecast.Unavailable("OpenMeteo"));

            var handler = BuildHandler(db.Object, forecast.Object, notifier);
            var result = await handler.Handle(
                new GetPivotForecastRequest { PivotId = 1, UserId = OwnerUserId },
                default);

            Assert.NotNull(result);
            Assert.True(result!.HasCoordinates);
            Assert.NotNull(result.Forecast);
            Assert.False(result.Forecast!.IsAvailable);
            Assert.NotNull(result.Message);
        }
    }
}
