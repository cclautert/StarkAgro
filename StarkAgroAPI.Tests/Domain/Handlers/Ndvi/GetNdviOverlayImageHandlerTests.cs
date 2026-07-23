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
    public class GetNdviOverlayImageHandlerTests
    {
        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        private static (GetNdviOverlayImageHandler handler, Mock<INdviOverlayImageService> svc) Build(
            List<MonitoredArea> areas, List<NdviReading> readings, byte[]? served, int userId = 42)
        {
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas);
            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, readings);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);

            var svc = new Mock<INdviOverlayImageService>();
            svc.Setup(s => s.GetOrCreateOverlayAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(served);

            return (new GetNdviOverlayImageHandler(db.Object, User(userId), svc.Object), svc);
        }

        [Fact]
        public async Task Overlay_Owner_ServicoDevolveBytes_Retorna200()
        {
            var (handler, _) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 3, AreaId = 5, UserId = 42, OverlayImageFileId = ObjectId.GenerateNewId() }],
                served: [1, 2, 3]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal([1, 2, 3], result!.Content);
            Assert.Equal("image/png", result.ContentType);
        }

        [Fact]
        public async Task Overlay_ReadingSemPngGerado_ServicoGeraSobDemanda_Retorna200()
        {
            // A DIFERENÇA do comportamento antigo: reading sem PNG NÃO é 404 na cara — o handler
            // delega ao serviço, que gera sob demanda. Aqui o serviço devolve o PNG recém-gerado.
            var reading = new NdviReading { Id = 3, AreaId = 5, UserId = 42, OverlayImageFileId = null };
            var (handler, svc) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [reading],
                served: [9, 9, 9]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal([9, 9, 9], result!.Content);
            svc.Verify(s => s.GetOrCreateOverlayAsync(
                It.Is<MonitoredArea>(a => a.Id == 5), It.Is<NdviReading>(r => r.Id == 3), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Overlay_AreaDeOutroUsuario_ReturnsNull_SemChamarServico()
        {
            var (handler, svc) = Build(
                areas: [], // outro tenant
                readings: [new NdviReading { Id = 3, AreaId = 5, UserId = 99, OverlayImageFileId = ObjectId.GenerateNewId() }],
                served: [1, 2, 3]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.Null(result);
            svc.Verify(s => s.GetOrCreateOverlayAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Overlay_ReadingDeOutraArea_ReturnsNull_SemChamarServico()
        {
            var (handler, svc) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [], // nenhum reading casa {Id, AreaId=5, UserId=42}
                served: [1, 2, 3]);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.Null(result);
            svc.Verify(s => s.GetOrCreateOverlayAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Overlay_ServicoDevolveNull_Retorna404()
        {
            // Nublada / kill-switch / PNG vazio → serviço devolve null → controller 404.
            var (handler, _) = Build(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 3, AreaId = 5, UserId = 42, CloudRejected = true }],
                served: null);

            var result = await handler.Handle(new GetNdviOverlayImageRequest { AreaId = 5, ReadingId = 3 }, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
