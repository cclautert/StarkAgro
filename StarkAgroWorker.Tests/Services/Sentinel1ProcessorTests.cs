using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Sentinel1;
using StarkAgroWorker.Services;
using StarkAgroWorker.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;

namespace StarkAgroWorker.Tests.Services
{
    public class Sentinel1ProcessorTests
    {
        private static MonitoredArea Area(int id = 1) =>
            new() { Id = id, UserId = 42, MonitoringEnabled = true, Name = "Talhão" };

        private sealed class Harness
        {
            public required Sentinel1Processor Processor { get; init; }
            public required Mock<ISentinel1FetchService> Fetch { get; init; }
        }

        private static Harness Build(PlatformAiSettings? settings, List<MonitoredArea> areas, Sentinel1FetchOutcome? outcome = null, bool fetchThrows = false)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);

            var fetch = new Mock<ISentinel1FetchService>();
            var setup = fetch.Setup(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()));
            if (fetchThrows) setup.ThrowsAsync(new InvalidOperationException("boom"));
            else setup.ReturnsAsync(outcome ?? new Sentinel1FetchOutcome(Sentinel1FetchStatus.Success, 1));

            var services = new ServiceCollection();
            services.AddScoped(_ => db.Object);
            services.AddScoped(_ => fetch.Object);

            return new Harness
            {
                Processor = new Sentinel1Processor(services.BuildServiceProvider(), NullLogger<Sentinel1Processor>.Instance),
                Fetch = fetch
            };
        }

        private static PlatformAiSettings Enabled() => new() { Id = 1, Sentinel1Enabled = true };

        [Fact]
        public async Task RunAsync_KillSwitchOff_NaoBusca()
        {
            var s = Enabled(); s.Sentinel1Enabled = false;
            var h = Build(s, [Area()]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_SemSettings_NaoQuebra()
        {
            var h = Build(settings: null, [Area()]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_Ligado_ChamaFetchPorArea()
        {
            var h = Build(Enabled(), [Area(1), Area(2)]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RunAsync_ErroNumaArea_NaoDerrubaOTick()
        {
            var h = Build(Enabled(), [Area()], fetchThrows: true);

            await h.Processor.RunAsync(CancellationToken.None); // não pode propagar

            h.Fetch.Verify(f => f.FetchAsync(It.IsAny<MonitoredArea>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
