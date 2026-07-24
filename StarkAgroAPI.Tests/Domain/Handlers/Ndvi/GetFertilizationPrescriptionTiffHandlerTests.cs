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
    public class GetFertilizationPrescriptionTiffHandlerTests
    {
        private static MonitoredArea Area(string? crop = "Café") =>
            new() { Id = 5, UserId = 42, Crop = crop };

        private static NdviReading Reading(bool cloud = false) =>
            new() { Id = 3, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 8), CloudRejected = cloud };

        private static FertilizationProfile Profile(int id = 1, string culture = "Café") =>
            new() { Id = id, Culture = culture };

        private static Mock<agpDBContext> Db(
            List<MonitoredArea>? areas, List<NdviReading>? readings, List<FertilizationProfile>? profiles)
        {
            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, areas ?? []);
            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, readings ?? []);
            var profilesCol = new Mock<IMongoCollection<FertilizationProfile>>();
            MongoMockHelper.SetupFindList(profilesCol, profiles ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);
            db.Setup(d => d.FertilizationProfiles).Returns(profilesCol.Object);
            return db;
        }

        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        private static (GetFertilizationPrescriptionTiffHandler h, Mock<INdviPrescriptionRasterService> svc, Notificator n) Build(
            Mock<agpDBContext> db, byte[]? tiff)
        {
            var svc = new Mock<INdviPrescriptionRasterService>();
            svc.Setup(s => s.GetTiffAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(),
                    It.IsAny<FertilizationProfile>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tiff);
            var n = new Notificator();
            return (new GetFertilizationPrescriptionTiffHandler(db.Object, User(), n, svc.Object), svc, n);
        }

        private static GetFertilizationPrescriptionTiffRequest Req(string? nutrient = null) =>
            new() { AreaId = 5, ReadingId = 3, Nutrient = nutrient };

        [Fact]
        public async Task Geotiff_AreaDeOutro_ReturnsNull_SemChamarServico()
        {
            var (h, svc, _) = Build(Db(areas: [], readings: [Reading()], profiles: [Profile()]), tiff: [1]);

            var result = await h.Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            svc.Verify(s => s.GetTiffAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(),
                It.IsAny<FertilizationProfile>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Geotiff_PassagemNublada_NotificaENull()
        {
            var (h, svc, n) = Build(Db(areas: [Area()], readings: [Reading(cloud: true)], profiles: [Profile()]), tiff: [1]);

            var result = await h.Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(n.HasNotification());
            svc.Verify(s => s.GetTiffAsync(It.IsAny<MonitoredArea>(), It.IsAny<NdviReading>(),
                It.IsAny<FertilizationProfile>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Geotiff_SemPerfil_NotificaENull()
        {
            var (h, _, n) = Build(Db(areas: [Area("Café")], readings: [Reading()], profiles: [Profile(culture: "Soja")]), tiff: [1]);

            var result = await h.Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(n.HasNotification());
        }

        [Fact]
        public async Task Geotiff_ServicoDevolveNull_Retorna404()
        {
            var (h, _, _) = Build(Db(areas: [Area()], readings: [Reading()], profiles: [Profile()]), tiff: null);

            Assert.Null(await h.Handle(Req(), CancellationToken.None));
        }

        [Fact]
        public async Task Geotiff_HappyPath_DevolveTiffEPropagaNutriente()
        {
            var (h, svc, _) = Build(Db(areas: [Area()], readings: [Reading()], profiles: [Profile()]), tiff: [4, 5, 6]);

            var result = await h.Handle(Req(nutrient: "n"), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal([4, 5, 6], result!.Content);
            Assert.Equal("image/tiff", result.ContentType);
            // "n" é normalizado para "N" antes de chegar ao serviço.
            svc.Verify(s => s.GetTiffAsync(It.Is<MonitoredArea>(a => a.Id == 5), It.Is<NdviReading>(r => r.Id == 3),
                It.Is<FertilizationProfile>(p => p.Id == 1), "N", It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
