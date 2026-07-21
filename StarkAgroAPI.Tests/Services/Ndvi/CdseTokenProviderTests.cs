using StarkAgroAPI.Services.Ndvi;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class CdseTokenProviderTests
    {
        private static HttpClient ClientReturning(HttpStatusCode code, string body, Mock<HttpMessageHandler> handler)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(code) { Content = new StringContent(body) });
            return new HttpClient(handler.Object) { BaseAddress = new Uri("https://identity.dataspace.copernicus.eu/") };
        }

        [Fact]
        public async Task GetToken_Success_ReturnsAndCaches()
        {
            var handler = new Mock<HttpMessageHandler>();
            var client = ClientReturning(HttpStatusCode.OK, """{"access_token":"abc","expires_in":600}""", handler);
            var provider = new CdseTokenProvider(client, new MemoryCache(new MemoryCacheOptions()), NullLogger<CdseTokenProvider>.Instance);

            var t1 = await provider.GetTokenAsync("cid", "secret", CancellationToken.None);
            var t2 = await provider.GetTokenAsync("cid", "secret", CancellationToken.None);

            Assert.Equal("abc", t1);
            Assert.Equal("abc", t2);
            // A segunda chamada veio do cache — só um POST de fato.
            handler.Protected().Verify("SendAsync", Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetToken_HttpError_ReturnsNull()
        {
            var handler = new Mock<HttpMessageHandler>();
            var client = ClientReturning(HttpStatusCode.Unauthorized, "nope", handler);
            var provider = new CdseTokenProvider(client, new MemoryCache(new MemoryCacheOptions()), NullLogger<CdseTokenProvider>.Instance);

            Assert.Null(await provider.GetTokenAsync("cid", "secret", CancellationToken.None));
        }

        [Fact]
        public async Task GetToken_TransportThrows_ReturnsNull()
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("down"));
            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://identity.dataspace.copernicus.eu/") };
            var provider = new CdseTokenProvider(client, new MemoryCache(new MemoryCacheOptions()), NullLogger<CdseTokenProvider>.Instance);

            Assert.Null(await provider.GetTokenAsync("cid", "secret", CancellationToken.None));
        }

        [Fact]
        public async Task GetToken_NoAccessTokenInBody_ReturnsNull()
        {
            var handler = new Mock<HttpMessageHandler>();
            var client = ClientReturning(HttpStatusCode.OK, """{"foo":"bar"}""", handler);
            var provider = new CdseTokenProvider(client, new MemoryCache(new MemoryCacheOptions()), NullLogger<CdseTokenProvider>.Instance);

            Assert.Null(await provider.GetTokenAsync("cid", "secret", CancellationToken.None));
        }
    }
}
