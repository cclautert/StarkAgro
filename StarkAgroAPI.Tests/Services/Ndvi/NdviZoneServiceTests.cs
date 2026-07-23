using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Moq;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class NdviZoneServiceTests
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
            public required NdviZoneService Svc { get; init; }
            public required Mock<ICdseProcessService> Process { get; init; }
            public required Mock<INdviOverlayStore> Store { get; init; }
            public required Mock<IMongoCollection<NdviReading>> Readings { get; init; }
        }

        private static Deps Build(
            PlatformAiSettings? settings, string? token, byte[]? tiff = null, byte[]? cached = null)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);
            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            readingsCol.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<NdviReading>>(),
                    It.IsAny<UpdateDefinition<NdviReading>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.NdviReadings).Returns(readingsCol.Object);

            var tokenProvider = new Mock<ICdseTokenProvider>();
            tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            var process = new Mock<ICdseProcessService>();
            process.Setup(p => p.GetNdviZonesTiffAsync(It.IsAny<string>(),
                    It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tiff);

            var store = new Mock<INdviOverlayStore>();
            store.Setup(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ObjectId.GenerateNewId());
            store.Setup(o => o.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cached);

            return new Deps
            {
                Svc = new NdviZoneService(db.Object, tokenProvider.Object, process.Object, store.Object, NullLogger<NdviZoneService>.Instance),
                Process = process, Store = store, Readings = readingsCol
            };
        }

        private static PlatformAiSettings Enabled() => new()
        {
            Id = 1, Sentinel2Enabled = true, CdseClientId = "id", CdseClientSecret = "secret"
        };

        [Fact]
        public async Task GetOrCreate_CacheHit_ServeDoStoreSemGerar()
        {
            var reading = new NdviReading { Id = 1, AreaId = 5, ZoneImageFileId = ObjectId.GenerateNewId() };
            var d = Build(Enabled(), token: "t", cached: [1, 2, 3]);

            var result = await d.Svc.GetOrCreateTiffAsync(Area(), reading, CancellationToken.None);

            Assert.Equal([1, 2, 3], result);
            d.Process.Verify(p => p.GetNdviZonesTiffAsync(It.IsAny<string>(),
                It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetOrCreate_CacheMiss_Gera_Grava_SetaFileId()
        {
            var reading = new NdviReading { Id = 1, AreaId = 5, AcquisitionDate = new DateTime(2026, 7, 18, 23, 24, 0, DateTimeKind.Utc) };
            var d = Build(Enabled(), token: "t", tiff: [9, 9, 9, 9]);

            var result = await d.Svc.GetOrCreateTiffAsync(Area(), reading, CancellationToken.None);

            Assert.Equal([9, 9, 9, 9], result);
            d.Store.Verify(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "image/tiff", It.IsAny<CancellationToken>()), Times.Once);
            d.Readings.Verify(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<NdviReading>>(),
                It.IsAny<UpdateDefinition<NdviReading>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetOrCreate_JanelaDoBucket_SemTruncarAHora()
        {
            // A geração usa AcquisitionDate verbatim (23:24), não .Date — a lição do overlay vazio.
            var acq = new DateTime(2026, 7, 18, 23, 24, 0, DateTimeKind.Utc);
            var reading = new NdviReading { Id = 1, AreaId = 5, AcquisitionDate = acq };
            var d = Build(Enabled(), token: "t", tiff: [1]);

            await d.Svc.GetOrCreateTiffAsync(Area(), reading, CancellationToken.None);

            d.Process.Verify(p => p.GetNdviZonesTiffAsync(It.IsAny<string>(),
                It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                acq, acq.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetOrCreate_KillSwitchOff_Null_NaoGera()
        {
            var s = Enabled(); s.Sentinel2Enabled = false;
            var reading = new NdviReading { Id = 1, AreaId = 5 };
            var d = Build(s, token: "t", tiff: [1]);

            Assert.Null(await d.Svc.GetOrCreateTiffAsync(Area(), reading, CancellationToken.None));
            d.Process.Verify(p => p.GetNdviZonesTiffAsync(It.IsAny<string>(),
                It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetOrCreate_ProcessDevolveNull_Null()
        {
            var reading = new NdviReading { Id = 1, AreaId = 5, AcquisitionDate = DateTime.UtcNow };
            var d = Build(Enabled(), token: "t", tiff: null);

            Assert.Null(await d.Svc.GetOrCreateTiffAsync(Area(), reading, CancellationToken.None));
            d.Store.Verify(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
