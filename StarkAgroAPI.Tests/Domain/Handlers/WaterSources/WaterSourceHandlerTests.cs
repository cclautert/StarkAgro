using StarkAgroAPI.Domain.Commands.Requests.WaterSources;
using StarkAgroAPI.Domain.Handlers.WaterSources;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.WaterSources
{
    public class WaterSourceHandlerTests
    {
        private static Mock<ICurrentUserContext> AuthUser(int userId = 1)
        {
            var m = new Mock<ICurrentUserContext>();
            m.Setup(c => c.UserId).Returns(userId);
            return m;
        }

        private static Mock<ICurrentUserContext> AnonUser()
        {
            var m = new Mock<ICurrentUserContext>();
            m.Setup(c => c.UserId).Returns((int?)null);
            return m;
        }

        private static Mock<agpDBContext> BuildCtx(
            Mock<IMongoCollection<WaterSource>>? wsColl = null,
            Mock<IMongoCollection<Pivot>>? pivotColl = null)
        {
            var ctx = new Mock<agpDBContext>();
            ctx.Setup(c => c.WaterSources).Returns((wsColl ?? new Mock<IMongoCollection<WaterSource>>()).Object);
            ctx.Setup(c => c.Pivots).Returns((pivotColl ?? new Mock<IMongoCollection<Pivot>>()).Object);
            ctx.Setup(c => c.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);
            return ctx;
        }

        // ─── CreateWaterSourceHandler ──────────────────────────────────────

        [Fact]
        public async Task Create_NullDbContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CreateWaterSourceHandler(null!, AuthUser().Object, new Mock<INotifier>().Object));
        }

        [Fact]
        public async Task Create_ValidRequest_ReturnsResponse()
        {
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            wsColl.Setup(c => c.InsertOneAsync(It.IsAny<WaterSource>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var ctx = BuildCtx(wsColl);
            var handler = new CreateWaterSourceHandler(ctx.Object, AuthUser().Object, new Mock<INotifier>().Object);
            var req = new CreateWaterSourceRequest { Name = "Well A", MaxFlowLitersPerHour = 500 };

            var result = await handler.Handle(req, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Well A", result!.Name);
            Assert.Equal(500, result.MaxFlowLitersPerHour);
        }

        [Fact]
        public async Task Create_EmptyName_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var ctx = BuildCtx();
            var handler = new CreateWaterSourceHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new CreateWaterSourceRequest { Name = "", MaxFlowLitersPerHour = 100 }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Create_ZeroFlow_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var ctx = BuildCtx();
            var handler = new CreateWaterSourceHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new CreateWaterSourceRequest { Name = "X", MaxFlowLitersPerHour = 0 }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Create_InvalidPivotIds_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var pivotColl = new Mock<IMongoCollection<Pivot>>();
            MongoMockHelper.SetupFindList(pivotColl, new List<Pivot>());
            var ctx = BuildCtx(pivotColl: pivotColl);
            var handler = new CreateWaterSourceHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new CreateWaterSourceRequest
            {
                Name = "X", MaxFlowLitersPerHour = 100, PivotIds = new List<int> { 99 }
            }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Create_UnauthenticatedUser_ThrowsInvalidOperation()
        {
            var ctx = BuildCtx();
            var handler = new CreateWaterSourceHandler(ctx.Object, AnonUser().Object, new Mock<INotifier>().Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new CreateWaterSourceRequest { Name = "X", MaxFlowLitersPerHour = 100 }, CancellationToken.None));
        }

        // ─── GetListWaterSourceHandler ─────────────────────────────────────

        [Fact]
        public async Task GetList_ReturnsUserWaterSources()
        {
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFindList(wsColl, new List<WaterSource>
            {
                new() { Id = 1, UserId = 1, Name = "W1", MaxFlowLitersPerHour = 100 },
                new() { Id = 2, UserId = 1, Name = "W2", MaxFlowLitersPerHour = 200 }
            });
            var ctx = BuildCtx(wsColl);
            var handler = new GetListWaterSourceHandler(ctx.Object, AuthUser().Object);

            var result = await handler.Handle(new GetListWaterSourceRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetList_UnauthenticatedUser_Throws()
        {
            var ctx = BuildCtx();
            var handler = new GetListWaterSourceHandler(ctx.Object, AnonUser().Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new GetListWaterSourceRequest(), CancellationToken.None));
        }

        [Fact]
        public void GetList_NullDbContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GetListWaterSourceHandler(null!, AuthUser().Object));
        }

        // ─── GetWaterSourceHandler ─────────────────────────────────────────

        [Fact]
        public async Task GetById_Found_ReturnsResponse()
        {
            var ws = new WaterSource { Id = 3, UserId = 1, Name = "W3", MaxFlowLitersPerHour = 300 };
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind(wsColl, ws);
            var ctx = BuildCtx(wsColl);
            var handler = new GetWaterSourceHandler(ctx.Object, AuthUser().Object, new Mock<INotifier>().Object);

            var result = await handler.Handle(new GetWaterSourceRequest { Id = 3 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("W3", result!.Name);
        }

        [Fact]
        public async Task GetById_NotFound_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind<WaterSource>(wsColl, null);
            var ctx = BuildCtx(wsColl);
            var handler = new GetWaterSourceHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new GetWaterSourceRequest { Id = 99 }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        // ─── EditWaterSourceHandler ────────────────────────────────────────

        [Fact]
        public async Task Edit_Valid_ReturnsUpdatedResponse()
        {
            var ws = new WaterSource { Id = 1, UserId = 1, Name = "Old", MaxFlowLitersPerHour = 100 };
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind(wsColl, ws);
            wsColl.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<WaterSource>>(),
                It.IsAny<UpdateDefinition<WaterSource>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            var ctx = BuildCtx(wsColl);
            var handler = new EditWaterSourceHandler(ctx.Object, AuthUser().Object, new Mock<INotifier>().Object);

            var result = await handler.Handle(new EditWaterSourceRequest { Id = 1, Name = "New", MaxFlowLitersPerHour = 200 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("New", result!.Name);
        }

        [Fact]
        public async Task Edit_EmptyName_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var ctx = BuildCtx();
            var handler = new EditWaterSourceHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new EditWaterSourceRequest { Id = 1, Name = "", MaxFlowLitersPerHour = 100 }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Edit_NotFound_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind<WaterSource>(wsColl, null);
            var ctx = BuildCtx(wsColl);
            var handler = new EditWaterSourceHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new EditWaterSourceRequest { Id = 99, Name = "X", MaxFlowLitersPerHour = 100 }, CancellationToken.None);

            Assert.Null(result);
        }

        // ─── DeleteWaterSourceHandler ──────────────────────────────────────

        [Fact]
        public async Task Delete_Existing_ReturnsTrue()
        {
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupDeleteOne(wsColl, 1);
            var ctx = BuildCtx(wsColl);
            var handler = new DeleteWaterSourceHandler(ctx.Object, AuthUser().Object, new Mock<INotifier>().Object);

            var result = await handler.Handle(new DeleteWaterSourceRequest { Id = 1 }, CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public async Task Delete_NotFound_NotifiesAndReturnsFalse()
        {
            var notifier = new Mock<INotifier>();
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupDeleteOne(wsColl, 0);
            var ctx = BuildCtx(wsColl);
            var handler = new DeleteWaterSourceHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new DeleteWaterSourceRequest { Id = 99 }, CancellationToken.None);

            Assert.False(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Delete_UnauthenticatedUser_Throws()
        {
            var ctx = BuildCtx();
            var handler = new DeleteWaterSourceHandler(ctx.Object, AnonUser().Object, new Mock<INotifier>().Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new DeleteWaterSourceRequest { Id = 1 }, CancellationToken.None));
        }
    }
}
