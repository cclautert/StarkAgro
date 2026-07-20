using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Handlers.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Ndvi
{
    public class GetNdviTrendHandlerTests
    {
        private static Mock<agpDBContext> Db(List<MonitoredArea>? areas = null, List<NdviReading>? readings = null)
        {
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas ?? []);
            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, readings ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);
            return db;
        }

        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        [Fact]
        public async Task Trend_AreaDoTenant_DevolveSerie()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings:
                [
                    new NdviReading { Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 3), NdviMean = 0.6 },
                    new NdviReading { Id = 2, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 8), CloudRejected = true, CloudCoveragePct = 100 }
                ]);
            var handler = new GetNdviTrendHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 5 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(5, result!.AreaId);
            Assert.Equal(2, result.Points.Count);
            Assert.True(result.Points[1].CloudRejected);
        }

        [Fact]
        public async Task Trend_AreaInexistenteOuDeOutro_NotificaENull()
        {
            var db = Db(areas: []); // área não é do tenant / não existe
            var notifier = new Notificator();
            var handler = new GetNdviTrendHandler(db.Object, User(42), notifier);

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 99 }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }
    }
}
