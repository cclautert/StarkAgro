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
    public class GetFertilizationPrescriptionHandlerTests
    {
        private static MonitoredArea Area(string? crop = "Café", int userId = 42)
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 }, new() { Lat = -22.99, Lng = -47.0 }
            }, out var geo, out _);
            return new MonitoredArea { Id = 5, UserId = userId, Crop = crop, Geometry = geo };
        }

        // Passagem com 2 classes: 25 px na 1ª (BareSoil), 75 px na última (High).
        private static NdviReading Reading() => new()
        {
            Id = 3, AreaId = 5, UserId = 42, AcquisitionDate = new DateTime(2026, 6, 8),
            CloudCoveragePct = 5,
            ClassCounts =
            [
                new NdviClassCount { Key = NdviClassification.Classes[0].Key, PixelCount = 25 },
                new NdviClassCount { Key = NdviClassification.Classes[^1].Key, PixelCount = 75 }
            ]
        };

        // Perfil com dose só na classe High (a BareSoil fica sem dose de propósito).
        private static FertilizationProfile CafeProfile(int id = 1, string culture = "Café") => new()
        {
            Id = id, Culture = culture,
            Doses = [new ZoneDose { ClassKey = NdviClassification.Classes[^1].Key, NitrogenKgHa = 90, PhosphorusKgHa = 40, PotassiumKgHa = 60 }]
        };

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

        private static GetFertilizationPrescriptionHandler Handler(Mock<agpDBContext> db, INotifier notifier, int userId = 42)
            => new(db.Object, User(userId), notifier);

        private static GetFertilizationPrescriptionRequest Req(int? profileId = null)
            => new() { AreaId = 5, ReadingId = 3, ProfileId = profileId };

        [Fact]
        public async Task Prescricao_AreaDeOutro_NotificaENull()
        {
            var db = Db(areas: [], readings: [Reading()], profiles: [CafeProfile()]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Prescricao_PassagemNublada_NotificaENull()
        {
            var r = Reading(); r.CloudRejected = true;
            var db = Db(areas: [Area()], readings: [r], profiles: [CafeProfile()]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Prescricao_SemClassCounts_NotificaENull()
        {
            var r = Reading(); r.ClassCounts = [];
            var db = Db(areas: [Area()], readings: [r], profiles: [CafeProfile()]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Prescricao_AreaSemCultura_NotificaENull()
        {
            var db = Db(areas: [Area(crop: null)], readings: [Reading()], profiles: [CafeProfile()]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Prescricao_SemPerfilDaCultura_NotificaENull()
        {
            // Área é "Café" mas só há perfil de "Soja" → sem match.
            var db = Db(areas: [Area()], readings: [Reading()], profiles: [CafeProfile(culture: "Soja")]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Prescricao_CulturaCasaIgnorandoCaseEEspaco()
        {
            var db = Db(areas: [Area(crop: "  café ")], readings: [Reading()], profiles: [CafeProfile(culture: "Café")]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Café", result!.Culture);
        }

        [Fact]
        public async Task Prescricao_ProfileIdOverride_VenceOAutoMatch()
        {
            // Perfil 7 é de outra cultura; passado explicitamente, é ele que vale.
            var db = Db(areas: [Area(crop: "Café")], readings: [Reading()],
                profiles: [CafeProfile(id: 1, culture: "Café"), CafeProfile(id: 7, culture: "Milho")]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(profileId: 7), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(7, result!.ProfileId);
            Assert.Equal("Milho", result.Culture);
        }

        [Fact]
        public async Task Prescricao_ProfileIdInexistente_NotificaENull()
        {
            var db = Db(areas: [Area()], readings: [Reading()], profiles: [CafeProfile(id: 1)]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(profileId: 999), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Prescricao_HappyPath_AritmeticaEDoseFaltante()
        {
            var area = Area();
            var db = Db(areas: [area], readings: [Reading()], profiles: [CafeProfile()]);
            var notifier = new Notificator();

            var result = await Handler(db, notifier).Handle(Req(), CancellationToken.None);

            Assert.NotNull(result);
            var p = result!;
            Assert.Equal(2, p.Zones.Count);

            var totalHa = AreaHectares.Of(area);
            Assert.Equal(Math.Round(totalHa, 3), p.TotalHectares, 3);

            var bare = p.Zones.Single(z => z.ClassKey == NdviClassification.Classes[0].Key);
            var high = p.Zones.Single(z => z.ClassKey == NdviClassification.Classes[^1].Key);

            // Percentuais pela distribuição de pixels (25/75).
            Assert.Equal(25.0, bare.Percent, 1);
            Assert.Equal(75.0, high.Percent, 1);

            // Soma dos hectares das zonas = área do talhão.
            Assert.Equal(p.TotalHectares, Math.Round(bare.Hectares + high.Hectares, 3), 2);

            // Classe sem dose no perfil → HasDose=false, tudo zero.
            Assert.False(bare.HasDose);
            Assert.Equal(0, bare.NitrogenKg);

            // Classe com dose → kg = dose × hectares (a menos do arredondamento).
            Assert.True(high.HasDose);
            Assert.Equal(90, high.NitrogenKgHa);
            Assert.Equal(Math.Round(90 * high.Hectares, 1), high.NitrogenKg, 1);

            // Total do talhão = soma das zonas (só a High tem dose).
            Assert.Equal(high.NitrogenKg, p.TotalNitrogenKg, 1);
            Assert.Equal(high.PhosphorusKg, p.TotalPhosphorusKg, 1);
            Assert.Equal(high.PotassiumKg, p.TotalPotassiumKg, 1);
        }
    }
}
