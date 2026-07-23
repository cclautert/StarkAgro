using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Services.Sentinel1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace StarkAgroAPI.Tests.Services.Sentinel1
{
    public class CdseSentinel1ServiceTests
    {
        private static MongoDB.Driver.GeoJsonObjectModel.GeoJsonPolygon<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates> Geo()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 }, new() { Lat = -22.99, Lng = -46.99 }
            }, out var g, out _);
            return g;
        }

        private static CdseSentinel1Service Service(HttpStatusCode code, string body, Exception? throws = null)
        {
            var handler = new Mock<HttpMessageHandler>();
            var setup = handler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            if (throws is not null) setup.ThrowsAsync(throws);
            else setup.ReturnsAsync(new HttpResponseMessage(code) { Content = new StringContent(body) });
            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://sh.dataspace.copernicus.eu/") };
            return new CdseSentinel1Service(client, NullLogger<CdseSentinel1Service>.Instance);
        }

        [Fact]
        public void BuildRequestBody_PedeSentinel1Grd_OrbitaFixa_SemNuvem()
        {
            var body = CdseSentinel1Service.BuildRequestBody(Geo(),
                new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc), "DESCENDING");

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("input").GetProperty("data")[0];
            Assert.Equal("sentinel-1-grd", data.GetProperty("type").GetString());
            var df = data.GetProperty("dataFilter");
            Assert.Equal("DESCENDING", df.GetProperty("orbitDirection").GetString());
            Assert.Equal("IW", df.GetProperty("acquisitionMode").GetString());
            Assert.Equal("DV", df.GetProperty("polarization").GetString());
            Assert.False(df.TryGetProperty("maxCloudCoverage", out _)); // radar não tem nuvem
            Assert.Equal("GAMMA0_ELLIPSOID", data.GetProperty("processing").GetProperty("backCoeff").GetString());
        }

        [Fact]
        public void Parse_ComTresSaidas_LeRviVvVh()
        {
            using var doc = JsonDocument.Parse(SampleResponse);

            var stat = Assert.Single(CdseSentinel1Service.Parse(doc.RootElement));

            Assert.Equal(new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc), stat.AcquisitionDate);
            Assert.Equal(0.51, stat.RviMean, 2);
            Assert.Equal(0.14, stat.VvMean, 2);
            Assert.Equal(0.021, stat.VhMean, 3);
            Assert.Equal(900, stat.ValidSampleCount);
        }

        [Fact]
        public void Parse_IntervaloSemPassagem_EhIgnorado()
        {
            // sampleCount 0 → buraco honesto, não um zero falso na série.
            using var doc = JsonDocument.Parse("""
            { "data": [ { "interval": { "from": "2026-06-20T00:00:00Z" },
              "outputs": { "rvi": { "bands": { "B0": { "stats": { "mean": 0, "sampleCount": 0 } } } } } } ] }
            """);

            Assert.Empty(CdseSentinel1Service.Parse(doc.RootElement));
        }

        [Fact]
        public void Parse_SemDataArray_Vazio()
        {
            using var doc = JsonDocument.Parse("""{ "status": "OK" }""");
            Assert.Empty(CdseSentinel1Service.Parse(doc.RootElement));
        }

        [Fact]
        public async Task GetStatistics_HttpError_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.BadRequest, "bad");
            Assert.Null(await svc.GetStatisticsAsync("t", Geo(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "DESCENDING", CancellationToken.None));
        }

        [Fact]
        public async Task GetStatistics_TransportThrows_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.OK, "", throws: new HttpRequestException("down"));
            Assert.Null(await svc.GetStatisticsAsync("t", Geo(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "DESCENDING", CancellationToken.None));
        }

        [Fact]
        public async Task GetStatistics_Success_Parseia()
        {
            var svc = Service(HttpStatusCode.OK, SampleResponse);
            var r = await svc.GetStatisticsAsync("t", Geo(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "DESCENDING", CancellationToken.None);
            Assert.NotNull(r);
            Assert.Single(r!);
        }

        private const string SampleResponse = """
        {
          "data": [
            { "interval": { "from": "2026-06-20T00:00:00Z", "to": "2026-06-21T00:00:00Z" },
              "outputs": {
                "rvi": { "bands": { "B0": { "stats": { "mean": 0.51, "sampleCount": 900 } } } },
                "vv":  { "bands": { "B0": { "stats": { "mean": 0.14, "sampleCount": 900 } } } },
                "vh":  { "bands": { "B0": { "stats": { "mean": 0.021, "sampleCount": 900 } } } }
              } }
          ],
          "status": "OK"
        }
        """;
    }
}
