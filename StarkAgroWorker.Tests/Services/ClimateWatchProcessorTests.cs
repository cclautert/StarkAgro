using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Forecast;
using StarkAgroWorker.Services;
using StarkAgroWorker.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;

namespace StarkAgroWorker.Tests.Services
{
    public class ClimateWatchProcessorTests
    {
        private static MonitoredArea Area(int id = 1) =>
            new() { Id = id, UserId = 42, MonitoringEnabled = true, Name = "Talhão", CenterLat = -22.5, CenterLng = -47.3 };

        private static DailyAgricultureData Day(int offsetDays, double tMin, double tMax) =>
            new(new DateOnly(2026, 7, 22).AddDays(offsetDays), tMax, tMin, 20, 0, null);

        private sealed class Harness
        {
            public required ClimateWatchProcessor Processor { get; init; }
            public required Mock<IAgricultureWeatherService> Weather { get; init; }
            public required Mock<IMongoCollection<ClimateAlert>> Alerts { get; init; }
            public required Mock<IPushNotificationService> Push { get; init; }
            public required List<ClimateAlert> Inserted { get; init; }
        }

        private static Harness Build(
            PlatformAiSettings? settings,
            List<MonitoredArea> areas,
            IReadOnlyList<DailyAgricultureData>? forecast,
            bool insertThrowsDuplicate = false,
            bool pushThrows = false)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);

            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas);

            var inserted = new List<ClimateAlert>();
            var alertsCol = new Mock<IMongoCollection<ClimateAlert>>();
            if (insertThrowsDuplicate)
            {
                alertsCol.Setup(c => c.InsertOneAsync(It.IsAny<ClimateAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(MongoTestErrors.DuplicateKey());
            }
            else
            {
                alertsCol.Setup(c => c.InsertOneAsync(It.IsAny<ClimateAlert>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                    .Callback<ClimateAlert, InsertOneOptions, CancellationToken>((a, _, _) => inserted.Add(a))
                    .Returns(Task.CompletedTask);
            }

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.ClimateAlerts).Returns(alertsCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var weather = new Mock<IAgricultureWeatherService>();
            weather.Setup(w => w.GetAgricultureDataAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(forecast);

            var push = new Mock<IPushNotificationService>();
            var pushSetup = push.Setup(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
            if (pushThrows) pushSetup.ThrowsAsync(new InvalidOperationException("push down"));
            else pushSetup.Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddScoped(_ => db.Object);
            services.AddScoped(_ => weather.Object);
            services.AddScoped(_ => push.Object);

            return new Harness
            {
                Processor = new ClimateWatchProcessor(services.BuildServiceProvider(), NullLogger<ClimateWatchProcessor>.Instance),
                Weather = weather,
                Alerts = alertsCol,
                Push = push,
                Inserted = inserted
            };
        }

        private static PlatformAiSettings Enabled(int frost = 3, int heat = 35) =>
            new() { Id = 1, ClimateAlertsEnabled = true, FrostAlertTempC = frost, HeatAlertTempC = heat };

        [Fact]
        public async Task RunAsync_KillSwitchOff_NaoBuscaPrevisao()
        {
            var s = Enabled(); s.ClimateAlertsEnabled = false;
            var h = Build(s, [Area()], [Day(0, 1, 20)]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Weather.Verify(w => w.GetAgricultureDataAsync(It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_MinimaAbaixoDoLimiar_GravaFrostRiskEEnviaPush()
        {
            var h = Build(Enabled(frost: 3), [Area()], [Day(0, 1.5, 22)]); // min 1,5 <= 3

            await h.Processor.RunAsync(CancellationToken.None);

            var alert = Assert.Single(h.Inserted);
            Assert.Equal(ClimateAlertType.Frost, alert.AlertType);
            Assert.Equal(1.5, alert.TemperatureC, 1);
            h.Push.Verify(p => p.SendAsync(42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_MaximaAcimaDoLimiar_GravaHeatRisk()
        {
            var h = Build(Enabled(heat: 35), [Area()], [Day(0, 18, 37)]); // max 37 >= 35

            await h.Processor.RunAsync(CancellationToken.None);

            Assert.Equal(ClimateAlertType.Heat, Assert.Single(h.Inserted).AlertType);
        }

        [Fact]
        public async Task RunAsync_TemperaturasNaFaixaSegura_NaoGravaNemPush()
        {
            var h = Build(Enabled(frost: 3, heat: 35), [Area()], [Day(0, 12, 28)]); // nem geada nem calor

            await h.Processor.RunAsync(CancellationToken.None);

            Assert.Empty(h.Inserted);
            h.Push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_HeatLimiarZeroDeDocLegado_CaiPara35_NaoDisparaSempre()
        {
            // HeatAlertTempC=0 (doc anterior à feature) NÃO pode fazer "máx >= 0" disparar sempre.
            var h = Build(Enabled(frost: 3, heat: 0), [Area()], [Day(0, 12, 28)]); // max 28 < 35 (fallback)

            await h.Processor.RunAsync(CancellationToken.None);

            Assert.Empty(h.Inserted);
        }

        [Fact]
        public async Task RunAsync_Dedup_MesmoRiscoJaGravado_NaoEnviaPush()
        {
            var h = Build(Enabled(frost: 3), [Area()], [Day(0, 1, 20)], insertThrowsDuplicate: true);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_PushFalha_NaoPropaga_AlertaJaGravado()
        {
            var h = Build(Enabled(frost: 3), [Area()], [Day(0, 1, 20)], pushThrows: true);

            await h.Processor.RunAsync(CancellationToken.None); // não pode subir

            Assert.Single(h.Inserted);
        }

        [Fact]
        public async Task RunAsync_AreaSemGeometriaNemCentro_EhPulada()
        {
            var area = new MonitoredArea { Id = 1, UserId = 42, MonitoringEnabled = true, Geometry = null! };
            var h = Build(Enabled(), [area], [Day(0, 1, 20)]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Weather.Verify(w => w.GetAgricultureDataAsync(It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_PrevisaoIndisponivel_NaoGravaAlertaFalso()
        {
            var h = Build(Enabled(), [Area()], forecast: null);

            await h.Processor.RunAsync(CancellationToken.None);

            Assert.Empty(h.Inserted);
            h.Push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_SemSettings_NaoQuebra()
        {
            var h = Build(settings: null, [Area()], [Day(0, 1, 20)]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Weather.Verify(w => w.GetAgricultureDataAsync(It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_ErroNumaArea_NaoDerrubaOTick()
        {
            var weather = new Mock<IAgricultureWeatherService>();
            weather.Setup(w => w.GetAgricultureDataAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, [Enabled()]);
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, [Area()]);
            var alertsCol = new Mock<IMongoCollection<ClimateAlert>>();

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.ClimateAlerts).Returns(alertsCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var push = new Mock<IPushNotificationService>();
            var services = new ServiceCollection();
            services.AddScoped(_ => db.Object);
            services.AddScoped(_ => weather.Object);
            services.AddScoped(_ => push.Object);
            var processor = new ClimateWatchProcessor(services.BuildServiceProvider(), NullLogger<ClimateWatchProcessor>.Instance);

            await processor.RunAsync(CancellationToken.None); // não pode propagar

            push.Verify(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
