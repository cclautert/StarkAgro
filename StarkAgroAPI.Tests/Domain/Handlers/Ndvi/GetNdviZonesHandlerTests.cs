using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Handlers.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Ndvi
{
    public class GetNdviZonesHandlerTests
    {
        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        private static (Mock<agpDBContext> db, Mock<INdviZoneService> zone) Db(
            List<MonitoredArea>? areas, List<NdviReading>? readings)
        {
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas ?? []);
            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, readings ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);
            return (db, new Mock<INdviZoneService>());
        }

        [Fact]
        public async Task Handle_DonoComOverlay_GeraEDevolveTiff()
        {
            var (db, zone) = Db(
                [new MonitoredArea { Id = 5, UserId = 42 }],
                [new NdviReading { Id = 1, AreaId = 5, UserId = 42, OverlayImageFileId = ObjectId.GenerateNewId() }]);
            zone.Setup(z => z.GetOrCreateTiffAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([1, 2, 3]);
            var handler = new GetNdviZonesHandler(db.Object, User(42), zone.Object);

            var result = await handler.Handle(new GetNdviZonesRequest { AreaId = 5, ReadingId = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("image/tiff", result!.ContentType);
            Assert.Equal([1, 2, 3], result.Content);
        }

        [Fact]
        public async Task Handle_AreaDeOutroUsuario_Null_NaoGera()
        {
            var (db, zone) = Db(areas: [], readings: []); // filtro por UserId → área não aparece
            var handler = new GetNdviZonesHandler(db.Object, User(99), zone.Object);

            var result = await handler.Handle(new GetNdviZonesRequest { AreaId = 5, ReadingId = 1 }, CancellationToken.None);

            Assert.Null(result);
            zone.Verify(z => z.GetOrCreateTiffAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ReadingSemOverlay_Null()
        {
            // Passagem nublada/legada (sem overlay) não rende raster de zonas.
            var (db, zone) = Db(
                [new MonitoredArea { Id = 5, UserId = 42 }],
                [new NdviReading { Id = 1, AreaId = 5, UserId = 42, OverlayImageFileId = null }]);
            var handler = new GetNdviZonesHandler(db.Object, User(42), zone.Object);

            var result = await handler.Handle(new GetNdviZonesRequest { AreaId = 5, ReadingId = 1 }, CancellationToken.None);

            Assert.Null(result);
            zone.Verify(z => z.GetOrCreateTiffAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_GeracaoFalha_Null()
        {
            var (db, zone) = Db(
                [new MonitoredArea { Id = 5, UserId = 42 }],
                [new NdviReading { Id = 1, AreaId = 5, UserId = 42, OverlayImageFileId = ObjectId.GenerateNewId() }]);
            zone.Setup(z => z.GetOrCreateTiffAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);
            var handler = new GetNdviZonesHandler(db.Object, User(42), zone.Object);

            Assert.Null(await handler.Handle(new GetNdviZonesRequest { AreaId = 5, ReadingId = 1 }, CancellationToken.None));
        }
    }
}
