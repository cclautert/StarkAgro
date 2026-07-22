using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Handlers.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Bson;
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
        public async Task Trend_ComOverlay_ExpoeBboxEOverlayReadingId()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 }, new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);

            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42, Geometry = geo }],
                readings:
                [
                    new NdviReading { Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 3) }, // sem overlay
                    new NdviReading { Id = 2, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 8), OverlayImageFileId = ObjectId.GenerateNewId() }
                ]);
            var handler = new GetNdviTrendHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 5 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result!.Points[0].OverlayReadingId);
            Assert.Null(result.Points[0].Bbox);
            Assert.Equal(2, result.Points[1].OverlayReadingId);
            Assert.NotNull(result.Points[1].Bbox);
            Assert.Equal(4, result.Points[1].Bbox!.Length);
            Assert.Equal(-47.0, result.Points[1].Bbox![0], 6); // minLng
        }

        [Fact]
        public async Task Trend_ComClassCounts_ProjetaClassesComRotuloCorEPercentual()
        {
            var classes = NdviClassification.Classes;
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings:
                [
                    new NdviReading
                    {
                        Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 3), NdviMean = 0.6,
                        // 100 pixels no total: 25 na primeira classe, 75 na última.
                        ClassCounts =
                        [
                            new NdviClassCount { Key = classes[0].Key, PixelCount = 25 },
                            new NdviClassCount { Key = classes[^1].Key, PixelCount = 75 }
                        ]
                    }
                ]);
            var handler = new GetNdviTrendHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 5 }, CancellationToken.None);

            var point = Assert.Single(result!.Points);
            // Sai sempre com as 6 classes na ordem da classificação, mesmo as ausentes (zeradas).
            Assert.Equal(classes.Count, point.Classes.Count);
            Assert.Equal(classes.Select(c => c.Key), point.Classes.Select(c => c.Key));
            Assert.Equal(25.0, point.Classes[0].Percent, 2);
            Assert.Equal(75.0, point.Classes[^1].Percent, 2);
            Assert.Equal(0.0, point.Classes[1].Percent, 2);
            Assert.Equal(100.0, point.Classes.Sum(c => c.Percent), 2);
            // Rótulo e cor vêm do servidor para o front não duplicar a tabela de cores do PNG.
            Assert.Equal(classes[0].Label, point.Classes[0].Label);
            Assert.Equal(classes[0].HexColor, point.Classes[0].Color);
            Assert.Equal(classes[0].HighEdge, point.Classes[0].MaxNdvi, 6);
        }

        [Fact]
        public async Task Trend_LeituraLegadaSemClassCounts_DevolveListaVazia()
        {
            // Documento gravado antes da classificação: não há migração, a tela cai no fallback.
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 3) }]);
            var handler = new GetNdviTrendHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 5 }, CancellationToken.None);

            Assert.Empty(Assert.Single(result!.Points).Classes);
        }

        [Fact]
        public async Task Trend_ClasseDesconhecidaNoDocumento_EIgnoradaSemVirarOutraClasse()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings:
                [
                    new NdviReading
                    {
                        Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 3),
                        ClassCounts =
                        [
                            new NdviClassCount { Key = "ClasseQueNaoExisteMais", PixelCount = 999 },
                            new NdviClassCount { Key = NdviClassification.Classes[2].Key, PixelCount = 10 }
                        ]
                    }
                ]);
            var handler = new GetNdviTrendHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 5 }, CancellationToken.None);

            var point = Assert.Single(result!.Points);
            Assert.Equal(NdviClassification.Classes.Count, point.Classes.Count);
            Assert.Equal(10, point.Classes[2].PixelCount);
            Assert.Equal(100.0, point.Classes[2].Percent, 2); // 999 órfão não entra no denominador
            Assert.DoesNotContain(point.Classes, c => c.PixelCount == 999);
        }

        [Fact]
        public async Task Trend_ProjetaNdreENdmiMean()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings:
                [
                    new NdviReading { Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 3),
                        NdviMean = 0.62, NdreMean = 0.28, NdmiMean = 0.15 }
                ]);
            var handler = new GetNdviTrendHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 5 }, CancellationToken.None);

            var point = Assert.Single(result!.Points);
            Assert.Equal(0.28, point.NdreMean, 6);
            Assert.Equal(0.15, point.NdmiMean, 6);
        }

        [Fact]
        public async Task Trend_LeituraLegadaSemIndicesExtras_NdreNdmiZero()
        {
            var db = Db(
                areas: [new MonitoredArea { Id = 5, UserId = 42 }],
                readings: [new NdviReading { Id = 1, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 3), NdviMean = 0.5 }]);
            var handler = new GetNdviTrendHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetNdviTrendRequest { AreaId = 5 }, CancellationToken.None);

            var point = Assert.Single(result!.Points);
            Assert.Equal(0, point.NdreMean);
            Assert.Equal(0, point.NdmiMean);
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
