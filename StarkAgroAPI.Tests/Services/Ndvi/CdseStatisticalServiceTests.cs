using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Ndvi;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class CdseStatisticalServiceTests
    {
        private static MongoDB.Driver.GeoJsonObjectModel.GeoJsonPolygon<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates> Geo()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 }, new() { Lat = -22.99, Lng = -46.99 }
            }, out var g, out _);
            return g;
        }

        private static CdseStatisticalService Service(HttpStatusCode code, string body, Exception? throws = null)
        {
            var handler = new Mock<HttpMessageHandler>();
            var setup = handler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            if (throws is not null) setup.ThrowsAsync(throws);
            else setup.ReturnsAsync(new HttpResponseMessage(code) { Content = new StringContent(body) });
            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://sh.dataspace.copernicus.eu/") };
            return new CdseStatisticalService(client, NullLogger<CdseStatisticalService>.Instance);
        }

        [Fact]
        public async Task GetStatistics_Success_ParsesSeries()
        {
            var svc = Service(HttpStatusCode.OK, SampleResponse);

            var stats = await svc.GetStatisticsAsync("tok", Geo(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, CancellationToken.None);

            Assert.NotNull(stats);
            Assert.Equal(2, stats!.Count);
        }

        [Fact]
        public async Task GetStatistics_HttpError_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.BadRequest, "bad");

            Assert.Null(await svc.GetStatisticsAsync("tok", Geo(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, CancellationToken.None));
        }

        [Fact]
        public async Task GetStatistics_TransportThrows_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.OK, "", throws: new HttpRequestException("down"));

            Assert.Null(await svc.GetStatisticsAsync("tok", Geo(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, CancellationToken.None));
        }

        private const string SampleResponse = """
        {
          "data": [
            { "interval": { "from": "2026-06-03T00:00:00Z", "to": "2026-06-04T00:00:00Z" },
              "outputs": { "ndvi": { "bands": { "B0": {
                "stats": { "min": 0.2, "max": 0.9, "mean": 0.65, "stDev": 0.12, "sampleCount": 1000, "noDataCount": 100 } } } } } },
            { "interval": { "from": "2026-06-08T00:00:00Z", "to": "2026-06-09T00:00:00Z" },
              "outputs": { "ndvi": { "bands": { "B0": {
                "stats": { "sampleCount": 0, "noDataCount": 0 } } } } } }
          ],
          "status": "OK"
        }
        """;

        [Fact]
        public void Parse_ValidInterval_ReadsStatsAndCloud()
        {
            using var doc = JsonDocument.Parse(SampleResponse);

            var stats = CdseStatisticalService.Parse(doc.RootElement);

            Assert.Equal(2, stats.Count);
            var first = stats[0];
            Assert.Equal(new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc), first.AcquisitionDate);
            Assert.Equal(0.65, first.Mean, 6);
            Assert.Equal(900, first.ValidSampleCount);        // 1000 - 100
            Assert.Equal(10.0, first.CloudPct, 6);            // 100/1000
        }

        [Fact]
        public void Parse_FullyCloudedInterval_IsZeroValidAndHundredCloud()
        {
            using var doc = JsonDocument.Parse(SampleResponse);

            var stats = CdseStatisticalService.Parse(doc.RootElement);

            var cloudy = stats[1];
            Assert.Equal(0, cloudy.ValidSampleCount);
            Assert.Equal(100.0, cloudy.CloudPct, 6);
        }

        [Fact]
        public void Parse_NoDataArray_ReturnsEmpty()
        {
            using var doc = JsonDocument.Parse("""{ "status": "OK" }""");

            Assert.Empty(CdseStatisticalService.Parse(doc.RootElement));
        }

        [Fact]
        public void BuildRequestBody_ContainsGeometryFilterAndEvalscript()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 },
                new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);

            var body = CdseStatisticalService.BuildRequestBody(
                geo, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.Contains("Polygon", body);
            Assert.Contains("sentinel-2-l2a", body);
            Assert.Contains("maxCloudCoverage", body);
            Assert.Contains("P1D", body);
            Assert.Contains("evaluatePixel", body);        // o evalscript foi embutido
            Assert.Contains("2026-06-01T00:00:00Z", body);
            Assert.Contains("-47", body);                   // longitude no ring (ordem [lng,lat])
        }

        [Fact]
        public void BuildRequestBody_AsksForTheClassHistogram_OnTheSameRequest()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 },
                new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);

            var body = CdseStatisticalService.BuildRequestBody(
                geo, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

            // O histograma viaja junto da estatística — é o que faz a classificação sair sem PU extra.
            Assert.Contains("\"calculations\"", body);
            Assert.Contains("\"histograms\"", body);
            Assert.Contains("\"default\"", body);           // `@default` em C# vira a chave "default"

            using var doc = JsonDocument.Parse(body);
            var h = doc.RootElement
                .GetProperty("calculations").GetProperty("ndvi")
                .GetProperty("histograms").GetProperty("default");

            // Histograma UNIFORME. A CDSE devolve 400 COMMON_BAD_PAYLOAD para um array explícito
            // de arestas em `bins` — foi assim que a busca de NDVI quebrou em produção.
            Assert.Equal(NdviClassification.HistogramBinCount, h.GetProperty("nBins").GetInt32());
            Assert.Equal(NdviClassification.HistogramLowEdge, h.GetProperty("lowEdge").GetDecimal());
            Assert.Equal(NdviClassification.HistogramHighEdge, h.GetProperty("highEdge").GetDecimal());
            Assert.False(h.TryGetProperty("bins", out _), "não enviar array de arestas — a CDSE rejeita");
        }

        [Fact]
        public void BuildRequestBody_ArestasDoHistogramaSaoFloatNoJson_NaoInteiro()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 },
                new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);

            var body = CdseStatisticalService.BuildRequestBody(
                geo, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

            // Asserção sobre o TEXTO, não sobre o valor: a CDSE infere o tipo do histograma pelo
            // literal JSON. `-1` (inteiro) → 400 "sampleType AUTO mis-matched with corresponding
            // histogram of type integer". `-1.0` (float) → 200. `double` em System.Text.Json
            // escreve `-1`; só `decimal` preserva o `.0`. Verificado contra a API real.
            Assert.Contains("\"lowEdge\":-1.0", body);
            Assert.Contains("\"highEdge\":1.0", body);
            Assert.DoesNotContain("\"lowEdge\":-1,", body);
            Assert.DoesNotContain("\"highEdge\":1,", body);
        }

        [Fact]
        public void Parse_ComHistogramaFino_AgregaBinsNasClasses()
        {
            using var doc = JsonDocument.Parse(HistogramResponse);

            var stats = CdseStatisticalService.Parse(doc.RootElement);

            var counts = Assert.Single(stats).ClassCounts;
            Assert.Equal(NdviClassification.Classes.Count, counts.Count);
            // Solo Exposto junta os dois bins abaixo de 0,20 (10 + 20); os demais caem sozinhos.
            Assert.Equal(new long[] { 30, 5, 0, 40, 0, 60 }, counts);
        }

        [Fact]
        public void Parse_WithoutHistogram_KeepsParsingAndLeavesClassesEmpty()
        {
            // Compat: resposta antiga (ou sem o bloco calculations) não pode quebrar a série.
            using var doc = JsonDocument.Parse(SampleResponse);

            var stats = CdseStatisticalService.Parse(doc.RootElement);

            Assert.Equal(2, stats.Count);
            Assert.All(stats, s => Assert.Empty(s.ClassCounts));
        }

        [Fact]
        public void ParseHistogram_BinNaFronteiraDaClasse_VaiParaAClasseCerta()
        {
            // [0.34,0.35) é Baixa; [0.35,0.36) é Média-Baixa. A agregação é por ponto médio
            // justamente para que ruído de ponto flutuante no lowEdge não troque a classe.
            using var doc = JsonDocument.Parse("""
            { "outputs": { "ndvi": { "bands": { "B0": { "histogram": { "bins": [
                { "lowEdge": 0.34, "highEdge": 0.35, "count": 7 },
                { "lowEdge": 0.35, "highEdge": 0.36, "count": 9 } ] } } } } } }
            """);

            var counts = CdseStatisticalService.ParseHistogram(doc.RootElement);

            Assert.Equal(7, counts[1]);   // Baixa
            Assert.Equal(9, counts[2]);   // Média-Baixa
        }

        [Fact]
        public void ParseHistogram_BinSemCount_EhIgnoradoSemQuebrar()
        {
            using var doc = JsonDocument.Parse("""
            { "outputs": { "ndvi": { "bands": { "B0": { "histogram": { "bins": [
                { "lowEdge": 0.10, "highEdge": 0.11 },
                { "lowEdge": 0.55, "highEdge": 0.56, "count": 12 } ] } } } } } }
            """);

            var counts = CdseStatisticalService.ParseHistogram(doc.RootElement);

            Assert.Equal(NdviClassification.Classes.Count, counts.Count);
            Assert.Equal(0, counts[0]);
            Assert.Equal(12, counts[3]);
        }

        [Fact]
        public void ParseHistogram_BinForaDoDominio_NaoEntraEmClasseNenhuma()
        {
            using var doc = JsonDocument.Parse("""
            { "outputs": { "ndvi": { "bands": { "B0": { "histogram": { "bins": [
                { "lowEdge": 5.0, "highEdge": 6.0, "count": 999 },
                { "lowEdge": 0.90, "highEdge": 0.91, "count": 4 } ] } } } } } }
            """);

            var counts = CdseStatisticalService.ParseHistogram(doc.RootElement);

            Assert.Equal(4, counts.Sum());   // o 999 fora de [-1,1] não é somado em lugar nenhum
        }

        // Histograma fino e uniforme, como a CDSE devolve com nBins/lowEdge/highEdge.
        private const string HistogramResponse = """
        {
          "data": [
            { "interval": { "from": "2026-06-03T00:00:00Z", "to": "2026-06-04T00:00:00Z" },
              "outputs": { "ndvi": { "bands": { "B0": {
                "stats": { "min": 0.1, "max": 0.95, "mean": 0.6, "stDev": 0.2, "sampleCount": 210, "noDataCount": 0 },
                "histogram": { "bins": [
                  { "lowEdge": -1.00, "highEdge": -0.99, "count": 10 },
                  { "lowEdge": 0.10, "highEdge": 0.11, "count": 20 },
                  { "lowEdge": 0.20, "highEdge": 0.21, "count": 5 },
                  { "lowEdge": 0.55, "highEdge": 0.56, "count": 40 },
                  { "lowEdge": 0.99, "highEdge": 1.00, "count": 60 } ],
                  "overflowCount": 0, "underflowCount": 0 } } } } } }
          ],
          "status": "OK"
        }
        """;
    }
}
