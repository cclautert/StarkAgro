using AgripeWebAPI.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Irrigation;
using AgripeWebAPI.Domain.Handlers.Irrigation;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Irrigation
{
    public class IrrigationHandlerTests
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

        private static IOptions<WeatherForecastSettings> DefaultSettings() =>
            Options.Create(new WeatherForecastSettings { RainThresholdMm = 5.0 });

        private static Mock<agpDBContext> BuildCtx(
            Mock<IMongoCollection<IrrigationProposal>>? proposalColl = null,
            Mock<IMongoCollection<WaterSource>>? wsColl = null,
            Mock<IMongoCollection<Pivot>>? pivotColl = null)
        {
            var ctx = new Mock<agpDBContext>();
            ctx.Setup(c => c.IrrigationProposals).Returns((proposalColl ?? new Mock<IMongoCollection<IrrigationProposal>>()).Object);
            ctx.Setup(c => c.WaterSources).Returns((wsColl ?? new Mock<IMongoCollection<WaterSource>>()).Object);
            ctx.Setup(c => c.Pivots).Returns((pivotColl ?? new Mock<IMongoCollection<Pivot>>()).Object);
            ctx.Setup(c => c.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);
            return ctx;
        }

        // ─── AcceptRejectProposalHandler ──────────────────────────────────────

        [Fact]
        public void AcceptReject_NullDbContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AcceptRejectProposalHandler(null!, AuthUser().Object, new Mock<INotifier>().Object));
        }

        [Fact]
        public async Task AcceptReject_UnauthenticatedUser_Throws()
        {
            var ctx = BuildCtx();
            var handler = new AcceptRejectProposalHandler(ctx.Object, AnonUser().Object, new Mock<INotifier>().Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new AcceptRejectProposalRequest { ProposalId = 1, Action = "accept" }, CancellationToken.None));
        }

        [Fact]
        public async Task AcceptReject_InvalidAction_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var ctx = BuildCtx();
            var handler = new AcceptRejectProposalHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new AcceptRejectProposalRequest { ProposalId = 1, Action = "approve" }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task AcceptReject_ProposalNotFound_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var proposalColl = new Mock<IMongoCollection<IrrigationProposal>>();
            MongoMockHelper.SetupFind<IrrigationProposal>(proposalColl, null);
            var ctx = BuildCtx(proposalColl);
            var handler = new AcceptRejectProposalHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new AcceptRejectProposalRequest { ProposalId = 99, Action = "accept" }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task AcceptReject_AlreadyDecided_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var proposal = new IrrigationProposal { Id = 1, UserId = 1, Status = ProposalStatus.Accepted };
            var proposalColl = new Mock<IMongoCollection<IrrigationProposal>>();
            MongoMockHelper.SetupFind(proposalColl, proposal);
            var ctx = BuildCtx(proposalColl);
            var handler = new AcceptRejectProposalHandler(ctx.Object, AuthUser().Object, notifier.Object);

            var result = await handler.Handle(new AcceptRejectProposalRequest { ProposalId = 1, Action = "reject" }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Accept_PendingProposal_ReturnsAcceptedResponse()
        {
            var proposal = new IrrigationProposal { Id = 2, UserId = 1, Status = ProposalStatus.Pending };
            var proposalColl = new Mock<IMongoCollection<IrrigationProposal>>();
            MongoMockHelper.SetupFind(proposalColl, proposal);
            proposalColl.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<IrrigationProposal>>(),
                    It.IsAny<UpdateDefinition<IrrigationProposal>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            var ctx = BuildCtx(proposalColl);
            var handler = new AcceptRejectProposalHandler(ctx.Object, AuthUser().Object, new Mock<INotifier>().Object);

            var result = await handler.Handle(new AcceptRejectProposalRequest { ProposalId = 2, Action = "accept" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result!.ProposalId);
            Assert.Equal("accepted", result.Status);
        }

        [Fact]
        public async Task Reject_PendingProposal_ReturnsRejectedResponse()
        {
            var proposal = new IrrigationProposal { Id = 3, UserId = 1, Status = ProposalStatus.Pending };
            var proposalColl = new Mock<IMongoCollection<IrrigationProposal>>();
            MongoMockHelper.SetupFind(proposalColl, proposal);
            proposalColl.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<IrrigationProposal>>(),
                    It.IsAny<UpdateDefinition<IrrigationProposal>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            var ctx = BuildCtx(proposalColl);
            var handler = new AcceptRejectProposalHandler(ctx.Object, AuthUser().Object, new Mock<INotifier>().Object);

            var result = await handler.Handle(new AcceptRejectProposalRequest { ProposalId = 3, Action = "REJECT" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("rejected", result!.Status);
        }

        // ─── ScheduleProposalHandler ──────────────────────────────────────────

        [Fact]
        public void Schedule_NullDbContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ScheduleProposalHandler(
                    null!,
                    AuthUser().Object,
                    new Mock<IWeatherForecastService>().Object,
                    new Mock<INotifier>().Object,
                    DefaultSettings()));
        }

        [Fact]
        public async Task Schedule_UnauthenticatedUser_Throws()
        {
            var ctx = BuildCtx();
            var handler = new ScheduleProposalHandler(
                ctx.Object,
                AnonUser().Object,
                new Mock<IWeatherForecastService>().Object,
                new Mock<INotifier>().Object,
                DefaultSettings());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new ScheduleProposalRequest { WaterSourceId = 1, TypicalDurationMinutes = 60 }, CancellationToken.None));
        }

        [Fact]
        public async Task Schedule_ZeroDuration_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var ctx = BuildCtx();
            var handler = new ScheduleProposalHandler(
                ctx.Object,
                AuthUser().Object,
                new Mock<IWeatherForecastService>().Object,
                notifier.Object,
                DefaultSettings());

            var result = await handler.Handle(new ScheduleProposalRequest { WaterSourceId = 1, TypicalDurationMinutes = 0 }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Schedule_WaterSourceNotFound_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind<WaterSource>(wsColl, null);
            var ctx = BuildCtx(wsColl: wsColl);
            var handler = new ScheduleProposalHandler(
                ctx.Object,
                AuthUser().Object,
                new Mock<IWeatherForecastService>().Object,
                notifier.Object,
                DefaultSettings());

            var result = await handler.Handle(new ScheduleProposalRequest { WaterSourceId = 99, TypicalDurationMinutes = 60 }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Schedule_WaterSourceNoPivots_NotifiesAndReturnsNull()
        {
            var notifier = new Mock<INotifier>();
            var ws = new WaterSource { Id = 1, UserId = 1, Name = "W1", MaxFlowLitersPerHour = 5000, PivotIds = new List<int>() };
            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind(wsColl, ws);
            var ctx = BuildCtx(wsColl: wsColl);
            var handler = new ScheduleProposalHandler(
                ctx.Object,
                AuthUser().Object,
                new Mock<IWeatherForecastService>().Object,
                notifier.Object,
                DefaultSettings());

            var result = await handler.Handle(new ScheduleProposalRequest { WaterSourceId = 1, TypicalDurationMinutes = 60 }, CancellationToken.None);

            Assert.Null(result);
            notifier.Verify(n => n.Handle(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task Schedule_ValidRequest_ReturnsProposalWithWindows()
        {
            var ws = new WaterSource { Id = 1, UserId = 1, Name = "W1", MaxFlowLitersPerHour = 5000, PivotIds = new List<int> { 10, 11 } };
            var pivots = new List<Pivot>
            {
                new() { Id = 10, UserId = 1, Name = "P10" },
                new() { Id = 11, UserId = 1, Name = "P11" }
            };

            var proposalColl = new Mock<IMongoCollection<IrrigationProposal>>();
            proposalColl.Setup(c => c.InsertOneAsync(It.IsAny<IrrigationProposal>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind(wsColl, ws);

            var pivotColl = new Mock<IMongoCollection<Pivot>>();
            MongoMockHelper.SetupFindList(pivotColl, pivots);

            var ctx = BuildCtx(proposalColl, wsColl, pivotColl);

            var forecast = new Mock<IWeatherForecastService>();
            forecast.Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WeatherForecast.Unavailable("test"));

            var handler = new ScheduleProposalHandler(
                ctx.Object,
                AuthUser().Object,
                forecast.Object,
                new Mock<INotifier>().Object,
                DefaultSettings());

            var result = await handler.Handle(new ScheduleProposalRequest { WaterSourceId = 1, TypicalDurationMinutes = 60 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(7, result!.ProposalId);
        }

        [Fact]
        public async Task Schedule_ForecastServiceThrows_StillSchedules()
        {
            var ws = new WaterSource { Id = 1, UserId = 1, Name = "W1", MaxFlowLitersPerHour = 5000, PivotIds = new List<int> { 10 } };
            var pivots = new List<Pivot> { new() { Id = 10, UserId = 1, Name = "P10", Latitude = -15.5, Longitude = -47.0 } };

            var proposalColl = new Mock<IMongoCollection<IrrigationProposal>>();
            proposalColl.Setup(c => c.InsertOneAsync(It.IsAny<IrrigationProposal>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var wsColl = new Mock<IMongoCollection<WaterSource>>();
            MongoMockHelper.SetupFind(wsColl, ws);

            var pivotColl = new Mock<IMongoCollection<Pivot>>();
            MongoMockHelper.SetupFindList(pivotColl, pivots);

            var ctx = BuildCtx(proposalColl, wsColl, pivotColl);

            var forecast = new Mock<IWeatherForecastService>();
            forecast.Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("timeout"));

            var handler = new ScheduleProposalHandler(
                ctx.Object,
                AuthUser().Object,
                forecast.Object,
                new Mock<INotifier>().Object,
                DefaultSettings());

            var result = await handler.Handle(new ScheduleProposalRequest { WaterSourceId = 1, TypicalDurationMinutes = 60 }, CancellationToken.None);

            Assert.NotNull(result);
        }
    }
}
