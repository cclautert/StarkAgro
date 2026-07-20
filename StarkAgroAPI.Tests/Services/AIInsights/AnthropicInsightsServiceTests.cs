using StarkAgroAPI.Configuration;
using StarkAgroAPI.Services.AIInsights;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace StarkAgroAPI.Tests.Services.AIInsights
{
    public class AnthropicInsightsServiceTests
    {
        private static readonly AISettings DefaultSettings = new()
        {
            Model = "gemini-1.5-flash",
            MaxTokens = 1024
        };

        private static AnthropicInsightsService BuildService(MockHttpMessageHandler handler)
        {
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/") };
            return new AnthropicInsightsService(
                http,
                Options.Create(DefaultSettings),
                NullLogger<AnthropicInsightsService>.Instance);
        }

        private static PivotAIContext BuildContext() => new()
        {
            PivotName = "Pivot Test",
            LimiteInferior = 25m,
            LimiteSuperior = 75m,
            ApiKeyOverride = "sk-ant-test-key",
            ModelOverride = "claude-haiku-4-5-20251001"
        };

        private const string AnthropicResponse = """
            {
              "id": "msg_test",
              "type": "message",
              "role": "assistant",
              "content": [
                { "type": "text", "text": "Recomendação: irrigar amanhã." }
              ]
            }
            """;

        [Fact]
        public async Task GetInsightsAsync_SuccessResponse_ReturnsText()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, AnthropicResponse);

            var sut = BuildService(handler);
            var result = await sut.GetInsightsAsync(BuildContext(), CancellationToken.None);

            Assert.Equal("Recomendação: irrigar amanhã.", result);
        }

        [Fact]
        public async Task GetInsightsAsync_HttpError_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""");

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
            var sut = new AnthropicInsightsService(
                http,
                Options.Create(DefaultSettings),
                NullLogger<AnthropicInsightsService>.Instance);

            var result = await sut.GetInsightsAsync(BuildContext(), CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetInsightsAsync_UsesModelOverride()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, AnthropicResponse);

            var sut = BuildService(handler);
            var ctx = BuildContext();
            ctx.ModelOverride = "claude-sonnet-4-6";

            var result = await sut.GetInsightsAsync(ctx, CancellationToken.None);

            Assert.NotNull(result);
        }

        private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw exception;
        }
    }
}
