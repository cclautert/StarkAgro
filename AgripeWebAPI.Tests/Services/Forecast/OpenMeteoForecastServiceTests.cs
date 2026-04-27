using AgripeWebAPI.Services.Forecast;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace AgripeWebAPI.Tests.Services.Forecast
{
    public class OpenMeteoForecastServiceTests
    {
        private static OpenMeteoForecastService BuildService(MockHttpMessageHandler handler)
        {
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com/") };
            return new OpenMeteoForecastService(http, NullLogger<OpenMeteoForecastService>.Instance);
        }

        [Fact]
        public async Task GetForecastAsync_ParsesDailyPrecipitationAndProbability()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "daily": {
                "time": ["2026-04-28","2026-04-29","2026-04-30"],
                "precipitation_sum": [1.5, 6.0, 0.0],
                "precipitation_probability_max": [40, 80, 10]
              }
            }
            """);

            var sut = BuildService(handler);

            var result = await sut.GetForecastAsync(-27.59, -48.55, 3, CancellationToken.None);

            Assert.True(result.IsAvailable);
            Assert.Equal("OpenMeteo", result.Source);
            Assert.Equal(7.5, result.TotalPrecipitationMm, 1);
            Assert.Equal(3, result.DailyForecasts.Count);
            Assert.Equal(80, result.DailyForecasts[1].ProbabilityPercent);
        }

        [Fact]
        public async Task GetForecastAsync_MissingDailyBlock_ReturnsUnavailable()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, "{}");

            var sut = BuildService(handler);

            var result = await sut.GetForecastAsync(-27.59, -48.55, 3, CancellationToken.None);

            Assert.False(result.IsAvailable);
            Assert.Equal("OpenMeteo", result.Source);
        }

        [Fact]
        public async Task GetForecastAsync_NullPrecipitationEntries_TreatedAsZero()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "daily": {
                "time": ["2026-04-28","2026-04-29"],
                "precipitation_sum": [3.0, null]
              }
            }
            """);

            var sut = BuildService(handler);

            var result = await sut.GetForecastAsync(-27.59, -48.55, 2, CancellationToken.None);

            Assert.True(result.IsAvailable);
            Assert.Equal(3.0, result.TotalPrecipitationMm, 1);
        }
    }
}
