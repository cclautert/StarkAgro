using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
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

        private sealed class Deps
        {
            public required NdviFetchService Svc { get; init; }
            public required Mock<IMongoCollection<NdviReading>> Readings { get; init; }
            public required Mock<ICdseProcessService> Process { get; init; }
            public required Mock<INdviOverlayStore> Overlay { get; init; }
        }

        private static Deps Build(
            PlatformAiSettings? settings,
            string? token,
            IReadOnlyList<NdviStat>? stats,
            int nextId = 1,
            byte[]? overlayPng = null)
        {
            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCol, settings is null ? [] : [settings]);

            var readingsCol = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(readingsCol, []);
            readingsCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<NdviReading>>(), It.IsAny<UpdateDefinition<NdviReading>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

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

            var processService = new Mock<ICdseProcessService>();
            processService.Setup(p => p.GetNdviOverlayPngAsync(It.IsAny<string>(),
                    It.IsAny<MongoDB.Driver.GeoJsonObjectModel.GeoJsonPolygon<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>>(),
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(overlayPng);

            var overlayStore = new Mock<INdviOverlayStore>();
            overlayStore.Setup(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ObjectId.GenerateNewId());

            var svc = new NdviFetchService(db.Object, tokenProvider.Object, statService.Object,
                processService.Object, overlayStore.Object, NullLogger<NdviFetchService>.Instance);
            return new Deps { Svc = svc, Readings = readingsCol, Process = processService, Overlay = overlayStore };
        }

        [Fact]
        public async Task Fetch_KillSwitchOff_Disabled()
        {
            var d = Build(settings: new PlatformAiSettings { Id = 1, Sentinel2Enabled = false }, token: "t", stats: []);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Disabled, outcome.Status);
            d.Readings.Verify(c => c.InsertOneAsync(It.IsAny<NdviReading>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Fetch_NoCredentials_Disabled()
        {
            var d = Build(settings: new PlatformAiSettings { Id = 1, Sentinel2Enabled = true }, token: "t", stats: []);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Disabled, outcome.Status);
        }

        [Fact]
        public async Task Fetch_TokenFails_Failed()
        {
            var d = Build(Enabled(), token: null, stats: []);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Failed, outcome.Status);
        }

        [Fact]
        public async Task Fetch_StatisticalFails_Failed()
        {
            var d = Build(Enabled(), token: "t", stats: null);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Failed, outcome.Status);
        }

        [Fact]
        public async Task Fetch_NewPass_InsereReadingEAvancaMaxDate()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
            };
            var d = Build(Enabled(), token: "t", stats: stats, nextId: 5);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Success, outcome.Status);
            Assert.Equal("2026-06-08", outcome.MaxAcquisitionDate);
            d.Readings.Verify(c => c.InsertOneAsync(
                It.Is<NdviReading>(r => r.AreaId == 1 && r.UserId == 42 && !r.CloudRejected && r.NdviMean == 0.65 && r.NdviCostCents == 2),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_ComHistograma_GravaClassCountsComChaveEstavel()
        {
            var classes = NdviClassification.Classes;
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
                {
                    ClassCounts = [10, 20, 30, 40, 50, 60]
                }
            };
            var d = Build(Enabled(), token: "t", stats: stats);

            await d.Svc.FetchAsync(Area(), CancellationToken.None);

            d.Readings.Verify(c => c.InsertOneAsync(
                It.Is<NdviReading>(r =>
                    r.ClassCounts.Count == classes.Count
                    && r.ClassCounts[0].Key == classes[0].Key && r.ClassCounts[0].PixelCount == 10
                    && r.ClassCounts[5].Key == classes[5].Key && r.ClassCounts[5].PixelCount == 60),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_SemHistograma_GravaClassCountsVazio()
        {
            // Resposta sem o bloco de histograma não pode inventar distribuição nem quebrar o fetch.
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
            };
            var d = Build(Enabled(), token: "t", stats: stats);

            await d.Svc.FetchAsync(Area(), CancellationToken.None);

            d.Readings.Verify(c => c.InsertOneAsync(
                It.Is<NdviReading>(r => r.ClassCounts.Count == 0),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_HistogramaComTamanhoErrado_EhDescartadoInteiro()
        {
            // Meia distribuição alinharia contagem com a classe errada — pior que nenhuma.
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
                {
                    ClassCounts = [10, 20]
                }
            };
            var d = Build(Enabled(), token: "t", stats: stats);

            await d.Svc.FetchAsync(Area(), CancellationToken.None);

            d.Readings.Verify(c => c.InsertOneAsync(
                It.Is<NdviReading>(r => r.ClassCounts.Count == 0),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_PassagemNublada_NaoGravaClassCountsZerados()
        {
            // O histograma de uma passagem nublada volta com as seis classes em zero. Gravar isso
            // faria o gráfico de composição despencar a 0% em todas as faixas — um vale que parece
            // perda de vigor. Nublada tem que ser buraco na série, não distribuição vazia.
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0, 0, 0, 0, 0, 100)
                {
                    ClassCounts = [0, 0, 0, 0, 0, 0]
                }
            };
            var d = Build(Enabled(), token: "t", stats: stats);

            await d.Svc.FetchAsync(Area(), CancellationToken.None);

            d.Readings.Verify(c => c.InsertOneAsync(
                It.Is<NdviReading>(r => r.CloudRejected && r.ClassCounts.Count == 0),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_CloudyPass_GravaCloudRejected()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0, 0, 0, 0, 0, 100)
            };
            var d = Build(Enabled(), token: "t", stats: stats);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Success, outcome.Status);
            d.Readings.Verify(c => c.InsertOneAsync(
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
            var d = Build(Enabled(), token: "t", stats: stats);

            var outcome = await d.Svc.FetchAsync(Area(lastAcq: "2026-06-05"), CancellationToken.None);

            Assert.Equal("2026-06-10", outcome.MaxAcquisitionDate);
            d.Readings.Verify(c => c.InsertOneAsync(It.IsAny<NdviReading>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_NewPass_GeraOverlayESetaFileId()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
            };
            var d = Build(Enabled(), token: "t", stats: stats, overlayPng: [1, 2, 3, 4]);

            await d.Svc.FetchAsync(Area(), CancellationToken.None);

            d.Process.Verify(p => p.GetNdviOverlayPngAsync(It.IsAny<string>(),
                It.IsAny<MongoDB.Driver.GeoJsonObjectModel.GeoJsonPolygon<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
            d.Overlay.Verify(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "image/png", It.IsAny<CancellationToken>()), Times.Once);
            d.Readings.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<NdviReading>>(), It.IsAny<UpdateDefinition<NdviReading>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_Overlay_PedeExatamenteOBucketDaStatisticalApi_SemTruncarParaMeiaNoite()
        {
            // Buckets da Statistical API são de 1 dia a partir do `timeRange.from`, que é a hora em
            // que o worker rodou — 23:24, não meia-noite. Truncar com `.Date` pedia
            // [18T00:00, 19T00:00), que NÃO contém a passagem real (19T de manhã): a CDSE devolvia
            // 200 com um PNG inteiramente transparente e o mapa ficava sem cor, sem erro nenhum.
            var acq = new DateTime(2026, 7, 18, 23, 24, 12, DateTimeKind.Utc);
            var stats = new List<NdviStat> { new(acq, 0.55, 0.2, 0.9, 0.1, 900, 0) };
            var d = Build(Enabled(), token: "t", stats: stats, overlayPng: [1, 2, 3, 4]);

            await d.Svc.FetchAsync(Area(), CancellationToken.None);

            d.Process.Verify(p => p.GetNdviOverlayPngAsync(
                It.IsAny<string>(),
                It.IsAny<MongoDB.Driver.GeoJsonObjectModel.GeoJsonPolygon<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>>(),
                acq,                 // início do bucket, com hora preservada
                acq.AddDays(1),      // fim do bucket
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Fetch_OverlayNulo_NaoSobeNemAtualiza()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
            };
            var d = Build(Enabled(), token: "t", stats: stats, overlayPng: null);

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Success, outcome.Status);
            d.Overlay.Verify(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            d.Readings.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<NdviReading>>(), It.IsAny<UpdateDefinition<NdviReading>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Fetch_OverlayVazio_NaoSobeNemAtualiza()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
            };
            var d = Build(Enabled(), token: "t", stats: stats, overlayPng: []); // PNG de 0 bytes

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            Assert.Equal(NdviFetchStatus.Success, outcome.Status);
            d.Overlay.Verify(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Fetch_OverlayThrows_NaoQuebraFetch()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0.65, 0.2, 0.9, 0.1, 900, 10)
            };
            var d = Build(Enabled(), token: "t", stats: stats, overlayPng: [1, 2, 3]);
            d.Overlay.Setup(o => o.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("GridFS down"));

            var outcome = await d.Svc.FetchAsync(Area(), CancellationToken.None);

            // Overlay é acessório: a tendência foi gravada e o outcome é Success mesmo com o PNG falhando.
            Assert.Equal(NdviFetchStatus.Success, outcome.Status);
        }

        [Fact]
        public async Task Fetch_SoNubladas_NaoGeraOverlay()
        {
            var stats = new List<NdviStat>
            {
                new(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), 0, 0, 0, 0, 0, 100)
            };
            var d = Build(Enabled(), token: "t", stats: stats, overlayPng: [1, 2, 3]);

            await d.Svc.FetchAsync(Area(), CancellationToken.None);

            d.Process.Verify(p => p.GetNdviOverlayPngAsync(It.IsAny<string>(),
                It.IsAny<MongoDB.Driver.GeoJsonObjectModel.GeoJsonPolygon<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
