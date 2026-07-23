using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Services.Sentinel1;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Moq;

namespace StarkAgroAPI.Tests.Services.Sentinel1
{
    public class Sentinel1FetchServiceTests
    {
        private static GeoJsonPolygon<GeoJson2DGeographicCoordinates> Geo()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 }, new() { Lat = -22.99, Lng = -46.99 }
            }, out var g, out _);
            return g;
        }

        private static MonitoredArea Area() => new() { Id = 5, UserId = 42, Geometry = Geo() };

        private sealed class Deps
        {
            public required Sentinel1FetchService Svc { get; init; }
            public required Mock<ICdseSentinel1Service> S1 { get; init; }
            public required Mock<IMongoCollection<Sentinel1Reading>> Readings { get; init; }
        }

        private static Deps Build(
            PlatformAiSettings? settings, string? token,
            IReadOnlyList<Sentinel1Stat>? stats, List<Sentinel1Reading>? existing = null, int nextId = 1)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);
            var readingsCol = new Mock<IMongoCollection<Sentinel1Reading>>();
            MongoMockHelper.SetupFindList(readingsCol, existing ?? []);
            readingsCol.Setup(c => c.InsertOneAsync(It.IsAny<Sentinel1Reading>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.Sentinel1Readings).Returns(readingsCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);

            var tokens = new Mock<ICdseTokenProvider>();
            tokens.Setup(t => t.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);

            var s1 = new Mock<ICdseSentinel1Service>();
            s1.Setup(s => s.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            return new Deps
            {
                Svc = new Sentinel1FetchService(db.Object, tokens.Object, s1.Object, NullLogger<Sentinel1FetchService>.Instance),
                S1 = s1, Readings = readingsCol
            };
        }

        private static PlatformAiSettings Enabled() => new()
        {
            Id = 1, Sentinel1Enabled = true, CdseClientId = "id", CdseClientSecret = "secret", Sentinel1CostCents = 2
        };

        [Fact]
        public async Task Fetch_KillSwitchOff_Disabled_NaoChamaCdse()
        {
            var s = Enabled(); s.Sentinel1Enabled = false;
            var d = Build(s, token: "t", stats: [new(DateTime.UtcNow, 0.5, 0.1, 0.02, 900)]);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(Sentinel1FetchStatus.Disabled, outcome.Status);
            d.S1.Verify(s => s.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Fetch_PassagemNova_GravaComOrbitaEDedup()
        {
            var acq = DateTime.UtcNow.AddDays(-2);
            var d = Build(Enabled(), token: "t", stats: [new(acq, 0.55, 0.14, 0.021, 900)]);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(Sentinel1FetchStatus.Success, outcome.Status);
            Assert.Equal(1, outcome.NewReadings);
            d.Readings.Verify(c => c.InsertOneAsync(
                It.Is<Sentinel1Reading>(r => r.AreaId == 5 && r.UserId == 42 && r.OrbitDirection == "DESCENDING"
                                             && r.RviMean == 0.55 && r.Sentinel1CostCents == 2),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_UltimaPassagemRecente_Skip_NaoChamaCdse()
        {
            // Última leitura há 1 dia (< 5) → pula a chamada à CDSE (não paga PU por nada novo).
            var existing = new List<Sentinel1Reading>
            {
                new() { Id = 1, AreaId = 5, AcquisitionDate = DateTime.UtcNow.AddDays(-1), OrbitDirection = "DESCENDING" }
            };
            var d = Build(Enabled(), token: "t", stats: [new(DateTime.UtcNow, 0.5, 0.1, 0.02, 900)], existing: existing);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(Sentinel1FetchStatus.Skipped, outcome.Status);
            d.S1.Verify(s => s.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Fetch_PassagemMaisVelhaQueAUltima_NaoRegrava()
        {
            var existing = new List<Sentinel1Reading>
            {
                new() { Id = 1, AreaId = 5, AcquisitionDate = DateTime.UtcNow.AddDays(-6), OrbitDirection = "DESCENDING" }
            };
            // Retorna uma passagem MAIS VELHA que a última — não deve gravar.
            var d = Build(Enabled(), token: "t",
                stats: [new(DateTime.UtcNow.AddDays(-8), 0.5, 0.1, 0.02, 900)], existing: existing);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(0, outcome.NewReadings);
            d.Readings.Verify(c => c.InsertOneAsync(It.IsAny<Sentinel1Reading>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Fetch_CdseFalha_Failed()
        {
            var d = Build(Enabled(), token: "t", stats: null);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(Sentinel1FetchStatus.Failed, outcome.Status);
        }
    }
}
