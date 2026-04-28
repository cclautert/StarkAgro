using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using AgripeWebAPI.Services.Forecast;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace AgripeWebAPI.Tests.Services.Forecast
{
    public class WeatherForecastOrchestratorTests
    {
        private static WeatherForecastOrchestrator Build(
            MockHttpMessageHandler openMeteoHandler,
            MockHttpMessageHandler googleHandler,
            WeatherForecastSettings settings,
            IMemoryCache? cache = null)
        {
            var openMeteoHttp = new HttpClient(openMeteoHandler) { BaseAddress = new Uri("https://api.open-meteo.com/") };
            var googleHttp = new HttpClient(googleHandler);

            var openMeteo = new OpenMeteoForecastService(openMeteoHttp, NullLogger<OpenMeteoForecastService>.Instance);
            var google = new GoogleWeatherAIForecastService(googleHttp, Options.Create(settings), NullLogger<GoogleWeatherAIForecastService>.Instance);

            cache ??= new MemoryCache(new MemoryCacheOptions());

            return new WeatherForecastOrchestrator(
                openMeteo, google, cache, Options.Create(settings),
                NullLogger<WeatherForecastOrchestrator>.Instance);
        }

        private static MockHttpMessageHandler OpenMeteoOk(double mm)
        {
            var h = new MockHttpMessageHandler();
            h.EnqueueResponse(HttpStatusCode.OK, $$"""
            {
              "daily": {
                "time": ["2026-04-28"],
                "precipitation_sum": [{{mm.ToString(System.Globalization.CultureInfo.InvariantCulture)}}],
                "precipitation_probability_max": [50]
              }
            }
            """);
            return h;
        }

        [Fact]
        public async Task GetForecastAsync_PrimarySucceeds_ReturnsResult()
        {
            var settings = new WeatherForecastSettings { PrimarySource = "OpenMeteo", FallbackSource = "OpenMeteo" };
            var orchestrator = Build(OpenMeteoOk(4.2), new MockHttpMessageHandler(), settings);

            var result = await orchestrator.GetForecastAsync(-27.5, -48.5, 1, CancellationToken.None);

            Assert.True(result.IsAvailable);
            Assert.Equal("OpenMeteo", result.Source);
            Assert.Equal(4.2, result.TotalPrecipitationMm, 1);
        }

        [Fact]
        public async Task GetForecastAsync_PrimaryThrows_FallsBackToSecondary()
        {
            var settings = new WeatherForecastSettings { PrimarySource = "GoogleWeatherAI", FallbackSource = "OpenMeteo" };
            // Google source will short-circuit to unavailable (no API key configured), forcing fallback
            var orchestrator = Build(OpenMeteoOk(2.0), new MockHttpMessageHandler(), settings);

            var result = await orchestrator.GetForecastAsync(-27.5, -48.5, 1, CancellationToken.None);

            Assert.True(result.IsAvailable);
            Assert.Equal("OpenMeteo", result.Source);
        }

        [Fact]
        public async Task GetForecastAsync_BothSourcesUnavailable_ReturnsUnavailable()
        {
            var settings = new WeatherForecastSettings { PrimarySource = "GoogleWeatherAI", FallbackSource = "GoogleWeatherAI" };
            var orchestrator = Build(new MockHttpMessageHandler(), new MockHttpMessageHandler(), settings);

            var result = await orchestrator.GetForecastAsync(-27.5, -48.5, 1, CancellationToken.None);

            Assert.False(result.IsAvailable);
        }

        [Fact]
        public async Task GetForecastAsync_PrimaryHttpFails_FallsBack()
        {
            var settings = new WeatherForecastSettings { PrimarySource = "OpenMeteo", FallbackSource = "GoogleWeatherAI" };
            var openMeteoHandler = new MockHttpMessageHandler();
            openMeteoHandler.EnqueueResponse(HttpStatusCode.InternalServerError, "boom");
            var orchestrator = Build(openMeteoHandler, new MockHttpMessageHandler(), settings);

            var result = await orchestrator.GetForecastAsync(-27.5, -48.5, 1, CancellationToken.None);

            // Fallback (GoogleWeatherAI) returns unavailable because key is placeholder
            Assert.False(result.IsAvailable);
        }

        [Fact]
        public async Task GetForecastAsync_CachesAvailableResults()
        {
            var settings = new WeatherForecastSettings { PrimarySource = "OpenMeteo", FallbackSource = "OpenMeteo", CacheDurationMinutes = 60 };
            var openMeteoHandler = OpenMeteoOk(3.3);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var orchestrator = Build(openMeteoHandler, new MockHttpMessageHandler(), settings, cache);

            var first = await orchestrator.GetForecastAsync(-27.5, -48.5, 1, CancellationToken.None);
            // The handler queue is now empty — second call must come from cache or it would fail
            var second = await orchestrator.GetForecastAsync(-27.5, -48.5, 1, CancellationToken.None);

            Assert.True(first.IsAvailable);
            Assert.True(second.IsAvailable);
            Assert.Equal(first.TotalPrecipitationMm, second.TotalPrecipitationMm);
        }
    }
}
