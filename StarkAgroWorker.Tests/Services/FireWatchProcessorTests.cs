using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Fire;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroWorker.Services;
using StarkAgroWorker.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;

namespace StarkAgroWorker.Tests.Services
{
    /// <summary>
    /// Constrói uma <see cref="MongoWriteException"/> de DuplicateKey sem depender do construtor
    /// interno do driver (mudou entre versões). O worker só olha
    /// <c>ex.WriteError?.Category == ServerErrorCategory.DuplicateKey</c>, então basta esse campo.
    /// </summary>
    internal static class MongoTestErrors
    {
        public static MongoWriteException DuplicateKey()
        {
            var writeError = (WriteError)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(WriteError));
            SetField(writeError, "_category", ServerErrorCategory.DuplicateKey);
            SetField(writeError, "_code", 11000);
            SetField(writeError, "_message", "E11000 duplicate key");

            var ex = (MongoWriteException)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(MongoWriteException));
            SetField(ex, "_writeError", writeError);
            return ex;
        }

        private static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            f?.SetValue(target, value);
        }
    }

    public class FireWatchProcessorTests
    {
        private static MonitoredArea Area(int id = 1)
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -21.96, Lng = -46.90 }, new() { Lat = -21.96, Lng = -46.89 },
                new() { Lat = -21.95, Lng = -46.89 }
            }, out var geo, out _);
            return new MonitoredArea { Id = id, UserId = 42, MonitoringEnabled = true, Name = "Talhão", Geometry = geo };
        }

        private static FireHotspotDto Hotspot(double lat = -21.955, double lng = -46.895, string sat = "N") =>
            new(lat, lng, new DateTime(2026, 7, 22, 7, 42, 0, DateTimeKind.Utc), sat, "nominal", 12.5);

        private sealed class Harness
        {
            public required FireWatchProcessor Processor { get; init; }
            public required Mock<IFirmsHotspotService> Firms { get; init; }
            public required Mock<IMongoCollection<FireHotspot>> Fire { get; init; }
            public required Mock<IPushNotificationService> Push { get; init; }
        }

        private static Harness Build(
            PlatformAiSettings? settings,
            List<MonitoredArea> areas,
            IReadOnlyList<FireHotspotDto>? hotspots,
            bool insertThrowsDuplicate = false,
            bool pushThrows = false)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);

            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas);

            var fireCol = new Mock<IMongoCollection<FireHotspot>>();
            if (insertThrowsDuplicate)
            {
                fireCol.Setup(c => c.InsertOneAsync(It.IsAny<FireHotspot>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(MongoTestErrors.DuplicateKey());
            }
            else
            {
                fireCol.Setup(c => c.InsertOneAsync(It.IsAny<FireHotspot>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
            }

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.FireHotspots).Returns(fireCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var firms = new Mock<IFirmsHotspotService>();
            firms.Setup(f => f.GetHotspotsAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<NdviBbox>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(hotspots);

            var push = new Mock<IPushNotificationService>();
            if (pushThrows)
                push.Setup(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("push down"));
            else
                push.Setup(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddScoped(_ => db.Object);
            services.AddScoped(_ => firms.Object);
            services.AddScoped(_ => push.Object);

            return new Harness
            {
                Processor = new FireWatchProcessor(services.BuildServiceProvider(), NullLogger<FireWatchProcessor>.Instance),
                Firms = firms,
                Fire = fireCol,
                Push = push
            };
        }

        private static PlatformAiSettings Enabled() => new()
        {
            Id = 1, FireAlertsEnabled = true, FirmsMapKey = "key", FireAlertRadiusKm = 10
        };

        [Fact]
        public async Task RunAsync_KillSwitchOff_NaoChamaOFirms()
        {
            var s = Enabled(); s.FireAlertsEnabled = false;
            var h = Build(s, [Area()], [Hotspot()]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Firms.Verify(f => f.GetHotspotsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NdviBbox>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_SemMapKey_NaoChamaOFirms()
        {
            var s = Enabled(); s.FirmsMapKey = null;
            var h = Build(s, [Area()], [Hotspot()]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Firms.Verify(f => f.GetHotspotsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NdviBbox>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_SemSettings_NaoQuebra()
        {
            var h = Build(settings: null, [Area()], [Hotspot()]);

            await h.Processor.RunAsync(CancellationToken.None); // não deve lançar

            h.Firms.Verify(f => f.GetHotspotsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NdviBbox>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_FocoNovo_GravaEEnviaUmPush()
        {
            var h = Build(Enabled(), [Area()], [Hotspot()]);

            await h.Processor.RunAsync(CancellationToken.None);

            // Duas fontes VIIRS → o mesmo foco pode vir das duas; o índice único trata a duplicata.
            h.Fire.Verify(c => c.InsertOneAsync(
                It.Is<FireHotspot>(f => f.AreaId == 1 && f.UserId == 42 && f.Satellite == "N"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            // Um push por área por tick — não um por foco.
            h.Push.Verify(p => p.SendAsync(42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void MongoTestErrors_DuplicateKey_TemACategoriaQueOWorkerFiltra()
        {
            // Sanidade do helper: se a categoria não for DuplicateKey, o catch do worker não casaria
            // e o teste de dedup passaria por engano (a exceção propagaria e o push também não sairia).
            var ex = MongoTestErrors.DuplicateKey();
            Assert.Equal(ServerErrorCategory.DuplicateKey, ex.WriteError?.Category);
        }

        [Fact]
        public async Task RunAsync_ReentregaDuplicada_NaoEnviaPush()
        {
            // Todo insert bate no índice único (foco já gravado) → nenhum foco novo → sem push.
            var h = Build(Enabled(), [Area()], [Hotspot()], insertThrowsDuplicate: true);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_PushFalha_NaoPropaga_FocoJaGravado()
        {
            var h = Build(Enabled(), [Area()], [Hotspot()], pushThrows: true);

            await h.Processor.RunAsync(CancellationToken.None); // a falha de push não pode subir

            h.Fire.Verify(c => c.InsertOneAsync(It.IsAny<FireHotspot>(),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RunAsync_SemFoco_NaoEnviaPush()
        {
            var h = Build(Enabled(), [Area()], []);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_ErroInesperadoNumaArea_EhEngolidoPeloCatchPorArea()
        {
            // FIRMS lançando (em vez de devolver null) simula um erro inesperado no processamento
            // da área. O catch por-área do RunAsync loga e NÃO propaga — um talhão problemático não
            // pode derrubar o tick inteiro.
            var firms = new Mock<IFirmsHotspotService>();
            firms.Setup(f => f.GetHotspotsAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<NdviBbox>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, [Enabled()]);
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, [Area()]);
            var fireCol = new Mock<IMongoCollection<FireHotspot>>();
            var push = new Mock<IPushNotificationService>();

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.FireHotspots).Returns(fireCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var services = new ServiceCollection();
            services.AddScoped(_ => db.Object);
            services.AddScoped(_ => firms.Object);
            services.AddScoped(_ => push.Object);
            var processor = new FireWatchProcessor(services.BuildServiceProvider(), NullLogger<FireWatchProcessor>.Instance);

            // Não deve lançar — se propagasse, o tick morreria e nenhuma outra área seria vista.
            await processor.RunAsync(CancellationToken.None);

            push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_AreaSemGeometria_EhPuladaSemChamarOFirms()
        {
            var area = new MonitoredArea { Id = 1, UserId = 42, MonitoringEnabled = true, Name = "Sem geo", Geometry = null! };
            var h = Build(Enabled(), [area], [Hotspot()]);

            await h.Processor.RunAsync(CancellationToken.None); // não deve estourar no ComputeBbox

            h.Firms.Verify(f => f.GetHotspotsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NdviBbox>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            h.Push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_FirmsRetornaNull_NaoGravaNemPush()
        {
            // Fonte fora do ar (null) não pode virar insert nem push — e não derruba o tick.
            var h = Build(Enabled(), [Area()], hotspots: null);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Fire.Verify(c => c.InsertOneAsync(It.IsAny<FireHotspot>(),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            h.Push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
