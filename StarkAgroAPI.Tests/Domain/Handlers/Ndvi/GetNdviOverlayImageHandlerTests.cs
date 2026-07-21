using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Handlers.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Ndvi
{
    public class GetNdviOverlayImageHandlerTests
    {
        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        private static (GetNdviOverlayImageHandler handler, Mock<INdviOverlayStore> store) Build(
            List<MonitoredArea> areas, List<NdviReading> readings, byte[]? downloaded, int userId = 42)
        {
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas);
            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, readings);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);

            var store = new Mock<INdviOverlayStore>();
            store.Setup(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>())).ReturnsAsync(downloaded);

            return (new GetNdviOverlayImageHandler(db.Object, User(userId), store.Object), store);
        }

        [Fact]
        public async Task Overlay_Owner_ReturnsBytes()
        {
            var fileId = ObjectId.GenerateNewId();
            var (handler, _) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 3, AreaId = 5, UserId = 42, OverlayImageFileId = fileId }],
                downloaded: [1, 2, 3]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal([1, 2, 3], result!.Content);
            Assert.Equal("image/png", result.ContentType);
        }

        [Fact]
        public async Task Overlay_AreaDeOutroUsuario_ReturnsNull()
        {
            // A área não é do chamador → filtro por UserId não a encontra.
            var (handler, store) = Build(
                areas: [], // outro tenant
                readings: [new NdviReading { Id = 3, AreaId = 5, UserId = 99, OverlayImageFileId = ObjectId.GenerateNewId() }],
                downloaded: [1, 2, 3]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.Null(result);
            store.Verify(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Overlay_ReadingDeOutraArea_ReturnsNull()
        {
            var (handler, store) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [], // nenhum reading casa {Id, AreaId=5, UserId=42}
                downloaded: [1, 2, 3]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.Null(result);
            store.Verify(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Overlay_SemPngGerado_ReturnsNull()
        {
            var (handler, store) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 3, AreaId = 5, UserId = 42, OverlayImageFileId = null }],
                downloaded: [1, 2, 3]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.Null(result);
            store.Verify(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Overlay_GridFsVazio_ReturnsNull()
        {
            var (handler, _) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 3, AreaId = 5, UserId = 42, OverlayImageFileId = ObjectId.GenerateNewId() }],
                downloaded: null); // arquivo sumiu do bucket

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
