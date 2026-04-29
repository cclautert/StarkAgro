using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class CreatePivotHandlerTests
    {
        private const int OwnerUserId = 42;

        private static (Mock<agpDBContext> Db, Mock<IMongoCollection<Pivot>> Pivots) BuildDbMocks(int nextId = 123)
        {
            var db = new Mock<agpDBContext>();
            var pivots = new Mock<IMongoCollection<Pivot>>();
            db.Setup(c => c.Pivots).Returns(pivots.Object);
            db.Setup(c => c.GetNextIdAsync("Pivot", It.IsAny<CancellationToken>())).ReturnsAsync(nextId);
            pivots.Setup(c => c.InsertOneAsync(It.IsAny<Pivot>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return (db, pivots);
        }

        private static Mock<ICurrentUserContext> BuildCurrentUser(int? userId = OwnerUserId)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(u => u.UserId).Returns(userId);
            return mock;
        }

        [Fact]
        public async Task Handle_NoCoordinates_CreatesPivotAndReturnsResponse()
        {
            var (db, pivots) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new CreatePivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(new CreatePivotRequest { Name = "TestPivot" }, default);

            Assert.NotNull(result);
            Assert.Equal(123, result!.Id);
            Assert.False(notifier.HasNotification());
            pivots.Verify(p => p.InsertOneAsync(
                It.Is<Pivot>(x => x.Name == "TestPivot" && x.UserId == OwnerUserId && x.LocationUpdatedAt == null),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithCoordinates_StampsLocationUpdatedAt()
        {
            var (db, pivots) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new CreatePivotHandler(db.Object, BuildCurrentUser().Object, notifier);
            var before = DateTime.UtcNow;

            var result = await handler.Handle(new CreatePivotRequest
            {
                Name = "Geo",
                Latitude = -29.7,
                Longitude = -53.7,
                Altitude = 95.0,
                LocationAddress = "Santa Maria, RS"
            }, default);

            Assert.NotNull(result);
            Assert.False(notifier.HasNotification());
            pivots.Verify(p => p.InsertOneAsync(
                It.Is<Pivot>(x => x.Latitude == -29.7
                    && x.Longitude == -53.7
                    && x.Altitude == 95.0
                    && x.LocationAddress == "Santa Maria, RS"
                    && x.LocationUpdatedAt.HasValue
                    && x.LocationUpdatedAt!.Value >= before),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(91.0, 0.0)]
        [InlineData(-91.0, 0.0)]
        [InlineData(0.0, 181.0)]
        [InlineData(0.0, -181.0)]
        public async Task Handle_OutOfRangeLatLon_NotifiesAndReturnsNull(double lat, double lon)
        {
            var (db, pivots) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new CreatePivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(
                new CreatePivotRequest { Name = "P", Latitude = lat, Longitude = lon },
                default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            pivots.Verify(p => p.InsertOneAsync(It.IsAny<Pivot>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(-501.0)]
        [InlineData(9001.0)]
        public async Task Handle_OutOfRangeAltitude_NotifiesAndReturnsNull(double altitude)
        {
            var (db, pivots) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new CreatePivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(
                new CreatePivotRequest { Name = "P", Latitude = 0, Longitude = 0, Altitude = altitude },
                default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            pivots.Verify(p => p.InsertOneAsync(It.IsAny<Pivot>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_LatitudeWithoutLongitude_NotifiesAndReturnsNull()
        {
            var (db, pivots) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new CreatePivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(
                new CreatePivotRequest { Name = "P", Latitude = 10.0 },
                default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            pivots.Verify(p => p.InsertOneAsync(It.IsAny<Pivot>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_NoAuthenticatedUser_Throws()
        {
            var (db, _) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new CreatePivotHandler(db.Object, BuildCurrentUser(null).Object, notifier);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new CreatePivotRequest { Name = "P" }, default));
        }
    }
}
