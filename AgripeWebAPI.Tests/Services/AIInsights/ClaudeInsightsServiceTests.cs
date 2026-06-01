using AgripeWebAPI.Configuration;
using AgripeWebAPI.Services.AIInsights;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace AgripeWebAPI.Tests.Services.AIInsights
{
    public class ClaudeInsightsServiceTests
    {
        private static readonly AISettings DefaultSettings = new()
        {
            AnthropicApiKey = "test-key",
            Model = "claude-sonnet-4-6",
            MaxTokens = 1024
        };

        private static ClaudeInsightsService BuildService(MockHttpMessageHandler handler)
        {
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
            return new ClaudeInsightsService(
                http,
                Options.Create(DefaultSettings),
                NullLogger<ClaudeInsightsService>.Instance);
        }

        private static PivotAIContext BuildContext(bool withReadings = false, bool withAnomalies = false, bool withForecast = false)
        {
            var ctx = new PivotAIContext
            {
                PivotName = "Pivot Test",
                LimiteInferior = 25m,
                LimiteSuperior = 75m,
                Latitude = -27.59,
                Longitude = -48.55,
                ForecastSummary = withForecast ? "Previsão: 10mm nos próximos 7 dias." : null
            };

            if (withReadings)
            {
                ctx.SensorReadings.Add(new SensorReadingContext
                {
                    SensorCode = "S01",
                    Quadrante = 1,
                    Readings =
                    [
                        new ReadingPoint { Value = 55.5m, Date = DateTime.UtcNow.AddHours(-1) },
                        new ReadingPoint { Value = 52.0m, Date = DateTime.UtcNow.AddHours(-2) }
                    ]
                });
            }

            if (withAnomalies)
            {
                ctx.RecentAnomalies.Add(new AnomalyContext
                {
                    SensorId = 10,
                    Value = 15m,
                    ExpectedMin = 25m,
                    ExpectedMax = 75m,
                    Date = DateTime.UtcNow.AddMinutes(-30)
                });
            }

            return ctx;
        }

        [Fact]
        public async Task GetInsightsAsync_SuccessResponse_ReturnsText()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
                {
                  "content": [
                    { "type": "text", "text": "Recomendação: irrigar amanhã." }
                  ]
                }
                """);

            var sut = BuildService(handler);
            var result = await sut.GetInsightsAsync(BuildContext(), CancellationToken.None);

            Assert.Equal("Recomendação: irrigar amanhã.", result);
        }

        [Fact]
        public async Task GetInsightsAsync_WithReadingsAndAnomalies_SuccessResponse_ReturnsText()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
                {
                  "content": [
                    { "type": "text", "text": "Anomalia detectada. Irrigar." }
                  ]
                }
                """);

            var sut = BuildService(handler);
            var result = await sut.GetInsightsAsync(BuildContext(withReadings: true, withAnomalies: true, withForecast: true), CancellationToken.None);

            Assert.Equal("Anomalia detectada. Irrigar.", result);
        }

        [Fact]
        public async Task GetInsightsAsync_HttpError_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"error": "unauthorized"}""");

            var sut = BuildService(handler);
            var result = await sut.GetInsightsAsync(BuildContext(), CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetInsightsAsync_InvalidJson_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, "not-valid-json{{{");

            var sut = BuildService(handler);
            var result = await sut.GetInsightsAsync(BuildContext(), CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetInsightsAsync_HttpException_ReturnsNull()
        {
            var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
            var sut = new ClaudeInsightsService(
                http,
                Options.Create(DefaultSettings),
                NullLogger<ClaudeInsightsService>.Instance);

            var result = await sut.GetInsightsAsync(BuildContext(), CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetInsightsAsync_ContextWithNoSensorReadings_SuccessResponse_ReturnsText()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
                {
                  "content": [
                    { "type": "text", "text": "Sem leituras. Verifique sensores." }
                  ]
                }
                """);

            var ctx = new PivotAIContext
            {
                PivotName = "Pivot Vazio",
                LimiteInferior = 25m,
                LimiteSuperior = 75m,
                SensorReadings =
                [
                    new SensorReadingContext { SensorCode = "S02", Quadrante = 2, Readings = [] }
                ]
            };

            var sut = BuildService(handler);
            var result = await sut.GetInsightsAsync(ctx, CancellationToken.None);

            Assert.Equal("Sem leituras. Verifique sensores.", result);
        }

        private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw exception;
        }
    }
}
