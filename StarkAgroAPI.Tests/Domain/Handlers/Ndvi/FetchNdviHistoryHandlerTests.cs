using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Handlers.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Ndvi
{
    public class FetchNdviHistoryHandlerTests
    {
        // Uma data seguramente no passado (o "hoje" dos testes muda; -60 dias nunca cai no futuro).
        private static DateTime PastDate => DateTime.UtcNow.Date.AddDays(-60);

        private static Mock<agpDBContext> Db(
            List<MonitoredArea>? areas = null,
            List<NdviReading>? readings = null,
            PlatformAiSettings? settings = null)
        {
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas ?? []);
            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, readings ?? []);
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            return db;
        }

        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        private static (Mock<INdviFetchService> fetch, Mock<INdviCostService> cost) Services(
            NdviHistoryOutcome? outcome = null, int monthCost = 0)
        {
            var fetch = new Mock<INdviFetchService>();
            fetch.Setup(f => f.FetchHistoricalAsync(It.IsAny<MonitoredArea>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(outcome ?? new NdviHistoryOutcome(NdviFetchStatus.Success, AcquisitionDates: []));
            var cost = new Mock<INdviCostService>();
            cost.Setup(c => c.GetCurrentMonthCostCentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(monthCost);
            return (fetch, cost);
        }

        private static PlatformAiSettings Enabled(int budget = 0) => new()
        {
            Id = 1, Sentinel2Enabled = true, CdseClientId = "cid", CdseClientSecret = "s", NdviCostCents = 1,
            NdviMonthlyBudgetCents = budget
        };

        private static FetchNdviHistoryRequest Req(DateTime date, int areaId = 5) => new() { AreaId = areaId, Date = date };

        [Fact]
        public async Task Historico_AreaDeOutro_NotificaENull_SemChamarServico()
        {
            var db = Db(areas: []); // área não é do tenant
            var notifier = new Notificator();
            var (fetch, cost) = Services();
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), notifier, fetch.Object, cost.Object);

            var result = await handler.Handle(Req(PastDate), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            fetch.Verify(f => f.FetchHistoricalAsync(It.IsAny<MonitoredArea>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Historico_DataFutura_NotificaENull()
        {
            var db = Db(areas: [new MonitoredArea { Id = 5, UserId = 42 }]);
            var notifier = new Notificator();
            var (fetch, cost) = Services();
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), notifier, fetch.Object, cost.Object);

            var result = await handler.Handle(Req(DateTime.UtcNow.Date.AddDays(3)), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            fetch.Verify(f => f.FetchHistoricalAsync(It.IsAny<MonitoredArea>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Historico_JanelaJaArmazenada_RetornaSemChamarCdse()
        {
            var date = PastDate;
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = date }]);
            var (fetch, cost) = Services();
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), new Notificator(), fetch.Object, cost.Object);

            var result = await handler.Handle(Req(date), CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result!.FetchedFromCdse);
            Assert.Equal(date.ToString("yyyy-MM-dd"), result.NearestDate);
            Assert.Single(result.AcquisitionDates);
            // Grátis: nem serviço de busca nem sequer a leitura do settings/custo.
            fetch.Verify(f => f.FetchHistoricalAsync(It.IsAny<MonitoredArea>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Historico_S2Desligado_NotificaENull()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [],
                settings: new PlatformAiSettings { Id = 1, Sentinel2Enabled = false });
            var notifier = new Notificator();
            var (fetch, cost) = Services();
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), notifier, fetch.Object, cost.Object);

            var result = await handler.Handle(Req(PastDate), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            fetch.Verify(f => f.FetchHistoricalAsync(It.IsAny<MonitoredArea>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Historico_TetoMensalAtingido_RecusaSemChamarCdse()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [],
                settings: Enabled(budget: 10));
            var notifier = new Notificator();
            var (fetch, cost) = Services(monthCost: 10); // já bateu o teto
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), notifier, fetch.Object, cost.Object);

            var result = await handler.Handle(Req(PastDate), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            fetch.Verify(f => f.FetchHistoricalAsync(It.IsAny<MonitoredArea>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Historico_HappyPath_ChamaCdseEDevolveDatasComMaisProxima()
        {
            var date = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [], // nada armazenado → tem que buscar
                settings: Enabled(budget: 100));
            var (fetch, cost) = Services(
                outcome: new NdviHistoryOutcome(NdviFetchStatus.Success, AcquisitionDates: ["2026-06-06", "2026-06-11"]),
                monthCost: 5);
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), new Notificator(), fetch.Object, cost.Object);

            var result = await handler.Handle(Req(date), CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.FetchedFromCdse);
            Assert.Equal(2, result.AcquisitionDates.Count);
            Assert.Equal("2026-06-06", result.NearestDate); // delta 2 < delta 3
            fetch.Verify(f => f.FetchHistoricalAsync(It.IsAny<MonitoredArea>(), date, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Historico_CdseSemPassagem_NotificaENull()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [],
                settings: Enabled(budget: 0));
            var notifier = new Notificator();
            var (fetch, cost) = Services(outcome: new NdviHistoryOutcome(NdviFetchStatus.Success, AcquisitionDates: []));
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), notifier, fetch.Object, cost.Object);

            var result = await handler.Handle(Req(PastDate), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Historico_CdseFalha_PropagaMensagem()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [],
                settings: Enabled(budget: 0));
            var notifier = new Notificator();
            var (fetch, cost) = Services(outcome: new NdviHistoryOutcome(NdviFetchStatus.Failed, "Falha na Statistical API da CDSE."));
            var handler = new FetchNdviHistoryHandler(db.Object, User(42), notifier, fetch.Object, cost.Object);

            var result = await handler.Handle(Req(PastDate), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }
    }
}
