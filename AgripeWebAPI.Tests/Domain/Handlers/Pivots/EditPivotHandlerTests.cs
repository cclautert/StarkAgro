using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class EditPivotHandlerTests
    {
        private const int OwnerUserId = 42;

        private static (Mock<agpDBContext> Db, Mock<IMongoCollection<Pivot>> Pivots) BuildDbMocks()
        {
            var db = new Mock<agpDBContext>();
            var pivots = new Mock<IMongoCollection<Pivot>>();
            db.Setup(c => c.Pivots).Returns(pivots.Object);
            pivots.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Pivot>>(),
                    It.IsAny<Pivot>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            return (db, pivots);
        }

        private static Mock<ICurrentUserContext> BuildCurrentUser(int? userId = OwnerUserId)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(u => u.UserId).Returns(userId);
            return mock;
        }

        [Fact]
        public async Task Handle_OwnedPivot_UpdatesAndReturnsResponse()
        {
            var (db, pivots) = BuildDbMocks();
            var existing = new Pivot { Id = 1, Name = "OldName", UserId = OwnerUserId };
            MongoMockHelper.SetupFind(pivots, existing);

            var notifier = new Notificator();
            var handler = new EditPivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(
                new EditPivotRequest { Id = 1, Name = "NewName" },
                default);

            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
            Assert.Equal("NewName", existing.Name);
            Assert.False(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_CoordinatesChanged_StampsLocationUpdatedAt()
        {
            var (db, pivots) = BuildDbMocks();
            var existing = new Pivot
            {
                Id = 1, Name = "P", UserId = OwnerUserId,
                Latitude = null, Longitude = null
            };
            MongoMockHelper.SetupFind(pivots, existing);

            var notifier = new Notificator();
            var handler = new EditPivotHandler(db.Object, BuildCurrentUser().Object, notifier);
            var before = DateTime.UtcNow;

            var result = await handler.Handle(
                new EditPivotRequest { Id = 1, Name = "P", Latitude = -29.7, Longitude = -53.7, Altitude = 100 },
                default);

            Assert.NotNull(result);
            Assert.True(existing.LocationUpdatedAt.HasValue);
            Assert.True(existing.LocationUpdatedAt!.Value >= before);
            Assert.Equal(-29.7, existing.Latitude);
            Assert.Equal(-53.7, existing.Longitude);
            Assert.Equal(100, existing.Altitude);
        }

        [Fact]
        public async Task Handle_CoordinatesUnchanged_KeepsLocationUpdatedAt()
        {
            var (db, pivots) = BuildDbMocks();
            var stamped = DateTime.UtcNow.AddDays(-2);
            var existing = new Pivot
            {
                Id = 1, Name = "P", UserId = OwnerUserId,
                Latitude = -29.7, Longitude = -53.7, Altitude = 100,
                LocationUpdatedAt = stamped
            };
            MongoMockHelper.SetupFind(pivots, existing);

            var notifier = new Notificator();
            var handler = new EditPivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(
                new EditPivotRequest { Id = 1, Name = "P2", Latitude = -29.7, Longitude = -53.7, Altitude = 100 },
                default);

            Assert.NotNull(result);
            Assert.Equal(stamped, existing.LocationUpdatedAt);
        }

        [Fact]
        public async Task Handle_PivotNotFound_NotifiesAndReturnsNull()
        {
            var (db, pivots) = BuildDbMocks();
            MongoMockHelper.SetupFind<Pivot>(pivots, null);

            var notifier = new Notificator();
            var handler = new EditPivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(new EditPivotRequest { Id = 999, Name = "X" }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_DifferentTenant_NotifiesAndReturnsNull()
        {
            // Tenant scoping: Find filter includes UserId so the lookup yields no record for the wrong user.
            var (db, pivots) = BuildDbMocks();
            MongoMockHelper.SetupFind<Pivot>(pivots, null);

            var notifier = new Notificator();
            var handler = new EditPivotHandler(db.Object, BuildCurrentUser(999).Object, notifier);

            var result = await handler.Handle(new EditPivotRequest { Id = 1, Name = "X" }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            pivots.Verify(p => p.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Pivot>>(),
                It.IsAny<Pivot>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_MissingId_NotifiesAndReturnsNull()
        {
            var (db, pivots) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new EditPivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(new EditPivotRequest { Name = "X" }, default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_OutOfRangeLatitude_NotifiesAndReturnsNull()
        {
            var (db, pivots) = BuildDbMocks();
            var notifier = new Notificator();
            var handler = new EditPivotHandler(db.Object, BuildCurrentUser().Object, notifier);

            var result = await handler.Handle(
                new EditPivotRequest { Id = 1, Name = "X", Latitude = 95, Longitude = 0 },
                default);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }
    }
}
