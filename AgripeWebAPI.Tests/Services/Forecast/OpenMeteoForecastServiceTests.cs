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

        [Fact]
        public async Task GetAgricultureDataAsync_ParsesTemperatureRadiationAndPrecipitationPerDay()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "daily": {
                "time": ["2026-04-28","2026-04-29","2026-04-30"],
                "temperature_2m_max": [32.5, 28.0, 29.1],
                "temperature_2m_min": [18.2, 17.5, 17.9],
                "shortwave_radiation_sum": [22.1, 15.0, 18.4],
                "precipitation_sum": [0.0, 20.5, 3.2],
                "precipitation_probability_max": [10, 90, 40]
              }
            }
            """);

            var sut = BuildService(handler);

            var result = await sut.GetAgricultureDataAsync(-27.59, -48.55, 3, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(3, result!.Count);

            Assert.Equal(new DateOnly(2026, 4, 28), result[0].Date);
            Assert.Equal(32.5, result[0].TempMax, 1);
            Assert.Equal(18.2, result[0].TempMin, 1);
            Assert.Equal(22.1, result[0].ShortwaveRadiationMJm2, 1);
            Assert.Equal(0.0, result[0].PrecipitationMm, 1);
            Assert.Equal(10, result[0].PrecipitationProbabilityPct);

            Assert.Equal(20.5, result[1].PrecipitationMm, 1);
            Assert.Equal(90, result[1].PrecipitationProbabilityPct);
        }

        [Fact]
        public async Task GetAgricultureDataAsync_MissingDaily_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, "{}");

            var sut = BuildService(handler);

            var result = await sut.GetAgricultureDataAsync(-27.59, -48.55, 1, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAgricultureDataAsync_HttpFailure_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.InternalServerError, "{}");

            var sut = BuildService(handler);

            var result = await sut.GetAgricultureDataAsync(-27.59, -48.55, 1, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetRecentPrecipitationAsync_SumsPastDaysAndToday_UsesPastDaysParam()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "daily": {
                "time": ["2026-06-30","2026-07-01","2026-07-02"],
                "precipitation_sum": [10.0, 5.5, 0.5]
              }
            }
            """);

            var sut = BuildService(handler);

            var result = await sut.GetRecentPrecipitationAsync(-27.59, -48.55, 2, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(16.0, result!.Value, 1);
            var uri = Assert.Single(handler.RequestedUris);
            Assert.Contains("past_days=2", uri!.ToString());
            Assert.Contains("forecast_days=1", uri.ToString());
        }

        [Fact]
        public async Task GetRecentPrecipitationAsync_NullEntries_TreatedAsZero()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "daily": {
                "time": ["2026-07-01","2026-07-02"],
                "precipitation_sum": [7.0, null]
              }
            }
            """);

            var sut = BuildService(handler);

            var result = await sut.GetRecentPrecipitationAsync(-27.59, -48.55, 1, CancellationToken.None);

            Assert.Equal(7.0, result!.Value, 1);
        }

        [Fact]
        public async Task GetRecentPrecipitationAsync_MissingDaily_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, "{}");

            var sut = BuildService(handler);

            var result = await sut.GetRecentPrecipitationAsync(-27.59, -48.55, 2, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetRecentPrecipitationAsync_HttpFailure_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.InternalServerError, "{}");

            var sut = BuildService(handler);

            var result = await sut.GetRecentPrecipitationAsync(-27.59, -48.55, 2, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
