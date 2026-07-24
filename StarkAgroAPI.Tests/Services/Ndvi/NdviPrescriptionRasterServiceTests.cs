using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Moq;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class NdviPrescriptionRasterServiceTests
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
        private static NdviReading Reading() => new() { Id = 3, AreaId = 5, AcquisitionDate = new DateTime(2026, 6, 8, 23, 24, 0, DateTimeKind.Utc) };
        private static FertilizationProfile Profile() => new()
        {
            Id = 1, Culture = "Café",
            Doses = [new ZoneDose { ClassKey = NdviClassification.Classes[^1].Key, NitrogenKgHa = 90 }]
        };

        private sealed class Deps
        {
            public required NdviPrescriptionRasterService Svc { get; init; }
            public required Mock<ICdseProcessService> Process { get; init; }
        }

        private static Deps Build(PlatformAiSettings? settings, string? token, byte[]? tiff)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);

            var tokenProvider = new Mock<ICdseTokenProvider>();
            tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

            var process = new Mock<ICdseProcessService>();
            process.Setup(p => p.GetPrescriptionTiffAsync(It.IsAny<string>(),
                    It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tiff);

            return new Deps
            {
                Svc = new NdviPrescriptionRasterService(db.Object, tokenProvider.Object, process.Object, NullLogger<NdviPrescriptionRasterService>.Instance),
                Process = process
            };
        }

        private static PlatformAiSettings Enabled() => new()
        {
            Id = 1, Sentinel2Enabled = true, CdseClientId = "id", CdseClientSecret = "secret"
        };

        [Fact]
        public async Task GetTiff_Sucesso_DevolveBytes()
        {
            var d = Build(Enabled(), token: "t", tiff: [1, 2, 3, 4]);

            var result = await d.Svc.GetTiffAsync(Area(), Reading(), Profile(), null, CancellationToken.None);

            Assert.Equal([1, 2, 3, 4], result);
        }

        [Fact]
        public async Task GetTiff_JanelaDoBucket_SemTruncarAHora()
        {
            var acq = Reading().AcquisitionDate; // 23:24
            var d = Build(Enabled(), token: "t", tiff: [1]);

            await d.Svc.GetTiffAsync(Area(), Reading(), Profile(), null, CancellationToken.None);

            d.Process.Verify(p => p.GetPrescriptionTiffAsync(It.IsAny<string>(),
                It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                acq, acq.AddDays(1), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetTiff_KillSwitchOff_Null_NaoChamaCdse()
        {
            var s = Enabled(); s.Sentinel2Enabled = false;
            var d = Build(s, token: "t", tiff: [1]);

            Assert.Null(await d.Svc.GetTiffAsync(Area(), Reading(), Profile(), null, CancellationToken.None));
            d.Process.Verify(p => p.GetPrescriptionTiffAsync(It.IsAny<string>(),
                It.IsAny<GeoJsonPolygon<GeoJson2DGeographicCoordinates>>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetTiff_TokenFalha_Null()
        {
            var d = Build(Enabled(), token: null, tiff: [1]);
            Assert.Null(await d.Svc.GetTiffAsync(Area(), Reading(), Profile(), null, CancellationToken.None));
        }

        [Fact]
        public async Task GetTiff_ProcessVazio_Null()
        {
            var d = Build(Enabled(), token: "t", tiff: []);
            Assert.Null(await d.Svc.GetTiffAsync(Area(), Reading(), Profile(), null, CancellationToken.None));
        }
    }
}
