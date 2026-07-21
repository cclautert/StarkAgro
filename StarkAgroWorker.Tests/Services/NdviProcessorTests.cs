using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroWorker.Services;
using StarkAgroWorker.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;

namespace StarkAgroWorker.Tests.Services
{
    public class NdviProcessorTests
    {
        private static MonitoredArea Area(int id = 1) => new()
        {
            Id = id, UserId = 42, MonitoringEnabled = true, Status = MonitoredAreaStatus.Idle
        };

        private sealed class Harness
        {
            public required Mock<IMongoCollection<MonitoredArea>> Areas { get; init; }
            public required Mock<INdviFetchService> Fetch { get; init; }
            public required NdviProcessor Processor { get; init; }
            public required List<UpdateDefinition<MonitoredArea>> Updates { get; init; }
        }

        private static Harness Build(
            bool enabled,
            List<MonitoredArea> claimed,
            NdviFetchOutcome? outcome = null,
            List<MonitoredArea>? stuck = null,
            int budgetCents = 0,
            int monthCostCents = 0)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol,
                [new PlatformAiSettings { Id = 1, Sentinel2Enabled = enabled, NdviMonthlyBudgetCents = budgetCents }]);

            var areas = new Mock<IMongoCollection<MonitoredArea>>();
            var queue = new Queue<MonitoredArea>(claimed);
            areas.Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<MonitoredArea>>(),
                    It.IsAny<UpdateDefinition<MonitoredArea>>(),
                    It.IsAny<FindOneAndUpdateOptions<MonitoredArea, MonitoredArea>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null!);
            MongoMockHelper.SetupFindList(areas, stuck ?? []);
            var updates = new List<UpdateDefinition<MonitoredArea>>();
            areas.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<MonitoredArea>>(), It.IsAny<UpdateDefinition<MonitoredArea>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<MonitoredArea>, UpdateDefinition<MonitoredArea>, UpdateOptions, CancellationToken>(
                    (_, u, _, _) => updates.Add(u))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.MonitoredAreas).Returns(areas.Object);

            var fetch = new Mock<INdviFetchService>();
            fetch.Setup(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(outcome ?? new NdviFetchOutcome(NdviFetchStatus.Success, MaxAcquisitionDate: "2026-06-10"));

            var costService = new Mock<INdviCostService>();
            costService.Setup(c => c.GetCurrentMonthCostCentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(monthCostCents);

            var services = new ServiceCollection();
            services.AddScoped(_ => db.Object);
            services.AddScoped(_ => fetch.Object);
            services.AddScoped(_ => costService.Object);

            return new Harness
            {
                Areas = areas,
                Fetch = fetch,
                Processor = new NdviProcessor(services.BuildServiceProvider(), NullLogger<NdviProcessor>.Instance),
                Updates = updates
            };
        }

        private static string Render(UpdateDefinition<MonitoredArea> u) =>
            u.Render(new RenderArgs<MonitoredArea>(
                MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<MonitoredArea>(),
                MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();

        [Fact]
        public async Task RunAsync_KillSwitchOff_DoesNothing()
        {
            var h = Build(enabled: false, claimed: [Area()]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Areas.Verify(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<MonitoredArea>>(), It.IsAny<UpdateDefinition<MonitoredArea>>(),
                It.IsAny<FindOneAndUpdateOptions<MonitoredArea, MonitoredArea>>(), It.IsAny<CancellationToken>()), Times.Never);
            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_BudgetHit_DoesNotClaim()
        {
            var h = Build(enabled: true, claimed: [Area(1)], budgetCents: 100, monthCostCents: 100); // custo >= teto

            await h.Processor.RunAsync(CancellationToken.None);

            h.Areas.Verify(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<MonitoredArea>>(), It.IsAny<UpdateDefinition<MonitoredArea>>(),
                It.IsAny<FindOneAndUpdateOptions<MonitoredArea, MonitoredArea>>(), It.IsAny<CancellationToken>()), Times.Never);
            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_UnderBudget_ClaimsNormally()
        {
            var h = Build(enabled: true, claimed: [Area(1)], budgetCents: 100, monthCostCents: 50); // custo < teto

            await h.Processor.RunAsync(CancellationToken.None);

            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_Success_CompletesAndSchedulesNextFetch()
        {
            var h = Build(enabled: true, claimed: [Area(1)],
                outcome: new NdviFetchOutcome(NdviFetchStatus.Success, MaxAcquisitionDate: "2026-06-10"));

            await h.Processor.RunAsync(CancellationToken.None);

            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Once);
            var complete = h.Updates.Select(Render).First();
            Assert.Contains("Idle", complete);
            Assert.Contains("NextFetchAt", complete);
            Assert.Contains("2026-06-10", complete); // LastAcquisitionDate avançou
        }

        [Fact]
        public async Task RunAsync_FetchFailed_SchedulesRetry()
        {
            var h = Build(enabled: true, claimed: [Area(1)],
                outcome: new NdviFetchOutcome(NdviFetchStatus.Failed, "boom"));

            await h.Processor.RunAsync(CancellationToken.None);

            var fail = h.Updates.Select(Render).First();
            Assert.Contains("NextAttemptAt", fail);
            Assert.Contains("RetryCount", fail);
        }

        [Fact]
        public async Task RunAsync_FetchThrows_IsCaughtAndScheduledForRetry()
        {
            var h = Build(enabled: true, claimed: [Area(1)]);
            h.Fetch.Setup(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            await h.Processor.RunAsync(CancellationToken.None);

            Assert.Contains(h.Updates.Select(Render), u => u.Contains("NextAttemptAt"));
        }

        [Fact]
        public async Task RunAsync_FetchDisabled_SnoozesArea()
        {
            var h = Build(enabled: true, claimed: [Area(1)],
                outcome: new NdviFetchOutcome(NdviFetchStatus.Disabled, "sem credencial"));

            await h.Processor.RunAsync(CancellationToken.None);

            var snooze = h.Updates.Select(Render).First();
            Assert.Contains("Idle", snooze);
            Assert.Contains("NextFetchAt", snooze);
        }

        [Fact]
        public async Task RunAsync_ReleasesZombie()
        {
            var h = Build(enabled: true, claimed: [], stuck: [new MonitoredArea { Id = 9, Status = MonitoredAreaStatus.Fetching }]);

            await h.Processor.RunAsync(CancellationToken.None);

            // O zumbi foi liberado via FailAsync (UpdateOne com backoff).
            Assert.Contains(h.Updates.Select(Render), u => u.Contains("NextAttemptAt"));
        }
    }
}
