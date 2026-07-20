using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class NdviFetchServiceTests
    {
        private static MonitoredArea Area(int id = 1, int userId = 42, string? lastAcq = null)
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 },
                new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);
            return new MonitoredArea { Id = id, UserId = userId, Geometry = geo, LastAcquisitionDate = lastAcq };
        }

        private static PlatformAiSettings Enabled() => new()
        {
            Id = 1, Sentinel2Enabled = true, CdseClientId = "cid", CdseClientSecret = "secret", NdviCostCents = 2
        };

        private static (NdviFetchService svc, Mock<IMongoCollection<NdviReading>> readings) Build(
            PlatformAiSettings? settings,
            string? token,
            IReadOnlyList<NdviStat>? stats,
            int nextId = 1)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);

            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);

            var tokenProvider = new Mock<ICdseTokenProvider>();
            tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            var statService = new Mock<ICdseStatisticalService>();
            statService.Setup(s => s.GetStatisticsAsync(It.IsAny<string>(),
                    It.IsAny<MongoDB.Driver.GeoJsonObjectModel.GeoJsonPolygon<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>>(),
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);

            var svc = new NdviFetchService(db.Object, tokenProvider.Object, statService.Object, NullLogger<NdviFetchService>.Instance);
            return (svc, readingsCol);
        }

        [Fact]
        public async Task Fetch_KillSwitchOff_Disabled()
        {
            var (svc, readings) = Build(settings: new PlatformAiSettings { Id = 1, Sentinel2Enabled = false }, token: "t", stats: []);

            var outcome = await svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Disabled, outcome.Status);
            readings.Verify(c => c.InsertOneAsync(It.IsAny<NdviReading>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Fetch_NoCredentials_Disabled()
        {
            var (svc, _) = Build(settings: new PlatformAiSettings { Id = 1, Sentinel2Enabled = true }, token: "t", stats: []);

            var outcome = await svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Disabled, outcome.Status);
        }

        [Fact]
        public async Task Fetch_TokenFails_Failed()
        {
            var (svc, _) = Build(Enabled(), token: null, stats: []);

            var outcome = await svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Failed, outcome.Status);
        }

        [Fact]
        public async Task Fetch_StatisticalFails_Failed()
        {
            var (svc, _) = Build(Enabled(), token: "t", stats: null);

            var outcome = await svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Failed, outcome.Status);
        }

        [Fact]
        public async Task Fetch_NewPass_InsereReadingEAvancaMaxDate()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
            };
            var (svc, readings) = Build(Enabled(), token: "t", stats: stats, nextId: 5);

            var outcome = await svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Success, outcome.Status);
            Assert.Equal("2026-06-08", outcome.MaxAcquisitionDate);
            readings.Verify(c => c.InsertOneAsync(
                It.Is<NdviReading>(r => r.AreaId == 1 && r.UserId == 42 && !r.CloudRejected && r.NdviMean == 0.65 && r.NdviCostCents == 2),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_CloudyPass_GravaCloudRejected()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0, 0, 0, 0, 0, 100)
            };
            var (svc, readings) = Build(Enabled(), token: "t", stats: stats);

            var outcome = await svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Success, outcome.Status);
            readings.Verify(c => c.InsertOneAsync(
                It.Is<NdviReading>(r => r.CloudRejected && r.CloudCoveragePct == 100),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_PassagemAntiga_EhIgnoradaPorDedup()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc), 0.6, 0.2, 0.9, 0.1, 900, 5),  // <= LastAcq
                new(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), 0.7, 0.3, 0.95, 0.1, 900, 5)  // > LastAcq
            };
            var (svc, readings) = Build(Enabled(), token: "t", stats: stats);

            var outcome = await svc.FetchAsync(Area(lastAcq: "2026-06-05"), CancellationToken.None);

            Assert.Equal("2026-06-10", outcome.MaxAcquisitionDate);
            readings.Verify(c => c.InsertOneAsync(It.IsAny<NdviReading>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
