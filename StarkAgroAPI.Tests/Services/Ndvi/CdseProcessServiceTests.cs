using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Ndvi;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver.GeoJsonObjectModel;
using Moq;
using Moq.Protected;
using System.Net;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class CdseProcessServiceTests
    {
        private static GeoJsonPolygon<GeoJson2DGeographicCoordinates> Geo()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 }, new() { Lat = -22.99, Lng = -46.99 }
            }, out var g, out _);
            return g;
        }

        private static CdseProcessService Service(HttpStatusCode code, byte[] body, Exception? throws = null)
        {
            var handler = new Mock<HttpMessageHandler>();
            var setup = handler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            if (throws is not null) setup.ThrowsAsync(throws);
            else setup.ReturnsAsync(new HttpResponseMessage(code) { Content = new ByteArrayContent(body) });
            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://sh.dataspace.copernicus.eu/") };
            return new CdseProcessService(client, NullLogger<CdseProcessService>.Instance);
        }

        [Fact]
        public async Task GetOverlay_Success_ReturnsPngBytes()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            var svc = Service(HttpStatusCode.OK, png);

            var bytes = await svc.GetNdviOverlayPngAsync("tok", Geo(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None);

            Assert.NotNull(bytes);
            Assert.Equal(png, bytes);
        }

        [Fact]
        public async Task GetOverlay_HttpError_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.BadRequest, System.Text.Encoding.UTF8.GetBytes("bad"));

            Assert.Null(await svc.GetNdviOverlayPngAsync("tok", Geo(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None));
        }

        [Fact]
        public async Task GetOverlay_TransportThrows_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.OK, [], throws: new HttpRequestException("down"));

            Assert.Null(await svc.GetNdviOverlayPngAsync("tok", Geo(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, CancellationToken.None));
        }

        [Fact]
        public void ComputeBbox_ReturnsMinMaxOfRing()
        {
            var bbox = CdseProcessService.ComputeBbox(Geo());

            Assert.Equal(-47.0, bbox.MinLng, 6);
            Assert.Equal(-46.99, bbox.MaxLng, 6);
            Assert.Equal(-23.0, bbox.MinLat, 6);
            Assert.Equal(-22.99, bbox.MaxLat, 6);
            Assert.Equal([-47.0, -23.0, -46.99, -22.99], bbox.ToArray());
        }

        [Fact]
        public void ResolveDimensions_WiderThanTall_LocksWidth()
        {
            // span lng (0.02) > span lat (0.01) → largura = 512, altura proporcional.
            var (w, h) = CdseProcessService.ResolveDimensions(new NdviBbox(-47.0, -23.0, -46.98, -22.99));

            Assert.Equal(512, w);
            Assert.Equal(256, h);
        }

        [Fact]
        public void ResolveDimensions_TallerThanWide_LocksHeight()
        {
            var (w, h) = CdseProcessService.ResolveDimensions(new NdviBbox(-47.0, -23.0, -46.99, -22.98));

            Assert.Equal(512, h);
            Assert.Equal(256, w);
        }

        [Fact]
        public void ResolveDimensions_DegenerateSpan_FallsBackToSquare()
        {
            var (w, h) = CdseProcessService.ResolveDimensions(new NdviBbox(-47.0, -23.0, -47.0, -23.0));

            Assert.Equal(512, w);
            Assert.Equal(512, h);
        }

        [Fact]
        public void BuildRequestBody_ContainsGeometryEvalscriptAndPngOutput()
        {
            var body = CdseProcessService.BuildRequestBody(
                Geo(), new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc), 512, 256);

            Assert.Contains("Polygon", body);
            Assert.Contains("sentinel-2-l2a", body);
            Assert.Contains("image/png", body);
            Assert.Contains("evaluatePixel", body);
            Assert.Contains("2026-06-08T00:00:00Z", body);
            Assert.Contains("-47", body); // longitude no ring (ordem [lng,lat])
        }

        [Fact]
        public void Evalscript_RampComesFromTheClassification_NotHardcoded()
        {
            var script = CdseProcessService.Evalscript;

            // O PNG tem que usar exatamente os cortes que a legenda anuncia; um ramp escrito à
            // mão aqui faria o mapa mentir sem nenhum teste reclamar.
            Assert.Contains(NdviClassification.BuildRampFunction(), script);
            Assert.Contains("function setup()", script);
            Assert.Contains("evaluatePixel", script);
            Assert.Contains("s.SCL === 3", script);   // máscara de nuvem preservada
            Assert.Contains("dataMask === 0", script);
        }

        [Fact]
        public void Evalscript_CallsRampExactlyOnceAndDefinesIt()
        {
            var script = CdseProcessService.Evalscript;

            Assert.Contains("function ramp(ndvi)", script);
            Assert.Contains("let c = ramp(ndvi);", script);
        }
    }
}
