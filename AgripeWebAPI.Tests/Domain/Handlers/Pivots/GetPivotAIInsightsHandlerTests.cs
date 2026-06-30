using AgripeWebAPI.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.AIInsights;
using AgripeWebAPI.Services.Forecast;
using AgripeWebAPI.Tests.Helpers;
using AgripeWebAPI.Tests.Mocks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetPivotAIInsightsHandlerTests : IDisposable
    {
        private readonly Mock<agpDBContext> _mockDb;
        private readonly Mock<IAIInsightsService> _mockAI;
        private readonly Mock<IAIInsightsServiceFactory> _mockFactory;
        private readonly Mock<IWeatherForecastService> _mockForecast;
        private readonly Mock<ICurrentUserContext> _mockUser;
        private readonly Mock<IMongoCollection<Pivot>> _mockPivots;
        private readonly Mock<IMongoCollection<User>> _mockUsers;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<IMongoCollection<SensorAnomaly>> _mockAnomalies;
        private readonly Mock<IMongoCollection<PlatformAiSettings>> _mockPlatformAiSettings;
        private readonly MockNotifier _notifier;
        private readonly IMemoryCache _cache;
        private readonly GetPivotAIInsightsHandler _handler;

        private static readonly Pivot DefaultPivot = new()
        {
            Id = 1, UserId = 42, Name = "Pivot Sul",
            LimiteInferior = 30m, LimiteSuperior = 70m
        };

        private static readonly User DefaultUser = new()
        {
            Id = 42, Name = "Fazendeiro", Email = "test@test.com", Password = "x"
        };

        private static readonly PlatformAiSettings DefaultAiSettings = new()
        {
            Id = 1, GeminiKey = "global-key", GeminiModel = "gemini-1.5-flash", ActiveProvider = "gemini"
        };

        public GetPivotAIInsightsHandlerTests()
        {
            _mockDb = new Mock<agpDBContext>();
            _mockAI = new Mock<IAIInsightsService>();
            _mockFactory = new Mock<IAIInsightsServiceFactory>();
            _mockForecast = new Mock<IWeatherForecastService>();
            _mockUser = new Mock<ICurrentUserContext>();
            _mockPivots = new Mock<IMongoCollection<Pivot>>();
            _mockUsers = new Mock<IMongoCollection<User>>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockAnomalies = new Mock<IMongoCollection<SensorAnomaly>>();
            _mockPlatformAiSettings = new Mock<IMongoCollection<PlatformAiSettings>>();
            _notifier = new MockNotifier();
            _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

            _mockDb.Setup(db => db.Pivots).Returns(_mockPivots.Object);
            _mockDb.Setup(db => db.Users).Returns(_mockUsers.Object);
            _mockDb.Setup(db => db.Sensors).Returns(_mockSensors.Object);
            _mockDb.Setup(db => db.ReadSensors).Returns(_mockReadSensors.Object);
            _mockDb.Setup(db => db.SensorAnomalies).Returns(_mockAnomalies.Object);
            _mockDb.Setup(db => db.PlatformAiSettings).Returns(_mockPlatformAiSettings.Object);
            _mockUser.Setup(u => u.UserId).Returns(42);

            MongoMockHelper.SetupFind(_mockPlatformAiSettings, DefaultAiSettings);

            _mockFactory.Setup(f => f.GetService(It.IsAny<string>())).Returns(_mockAI.Object);
            _mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Recomendação: irrigar o pivot.");

            _mockForecast.Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WeatherForecast.Unavailable("test"));

            _handler = CreateHandler();
        }

        public void Dispose() => _cache.Dispose();

        private GetPivotAIInsightsHandler CreateHandler() => new(
            _mockDb.Object,
            _mockFactory.Object,
            _mockForecast.Object,
            _mockUser.Object,
            _notifier,
            _cache,
            Options.Create(new AISettings { CacheDurationMinutes = 30, Model = "gemini-1.5-flash", MaxTokens = 1024 }),
            Mock.Of<ILogger<GetPivotAIInsightsHandler>>());

        private void SetupHappyPath()
        {
            MongoMockHelper.SetupFind(_mockPivots, DefaultPivot);
            MongoMockHelper.SetupFind(_mockUsers, DefaultUser);
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor>());
            MongoMockHelper.SetupFindList(_mockAnomalies, new List<SensorAnomaly>());
        }

        [Fact]
        public async Task Handle_InvalidPivotId_ShouldNotifyAndReturnNull()
        {
            var request = new GetPivotAIInsightsRequest { PivotId = 0, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Null(result);
            Assert.True(_notifier.HasNotification());
            _mockAI.Verify(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_PivotNotFound_ShouldNotifyAndReturnNull()
        {
            MongoMockHelper.SetupFind<Pivot>(_mockPivots, null);

            var request = new GetPivotAIInsightsRequest { PivotId = 99, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Null(result);
            Assert.True(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_HappyPath_ShouldReturnInsights()
        {
            SetupHappyPath();

            var request = new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Recomendação: irrigar o pivot.", result.Insights);
            Assert.False(result.FromCache);
            Assert.False(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_HappyPath_ShouldCallAIService()
        {
            SetupHappyPath();

            var request = new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 };
            await _handler.Handle(request, CancellationToken.None);

            _mockAI.Verify(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_SecondCall_ShouldReturnFromCache()
        {
            SetupHappyPath();
            var request = new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 };

            await _handler.Handle(request, CancellationToken.None);
            var second = await _handler.Handle(request, CancellationToken.None);

            Assert.NotNull(second);
            Assert.True(second.FromCache);
            // AI service called only once (second came from cache)
            _mockAI.Verify(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_AIServiceReturnsNull_ShouldNotifyAndReturnNull()
        {
            SetupHappyPath();
            _mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            var request = new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Null(result);
            Assert.True(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_TenantIsolation_PivotBelongsToDifferentUser_ShouldReturnNull()
        {
            // Pivot lookup with different UserId returns null
            MongoMockHelper.SetupFind<Pivot>(_mockPivots, null);

            var request = new GetPivotAIInsightsRequest { PivotId = 1, UserId = 99 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Null(result);
            Assert.True(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_HappyPath_ResponseHasGeneratedAt()
        {
            SetupHappyPath();
            var before = DateTime.UtcNow;

            var result = await _handler.Handle(
                new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.GeneratedAt >= before);
        }

        [Fact]
        public async Task Handle_ContextIncludesPivotLimits()
        {
            SetupHappyPath();
            PivotAIContext? capturedContext = null;
            _mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()))
                .Callback<PivotAIContext, CancellationToken>((ctx, _) => capturedContext = ctx)
                .ReturnsAsync("ok");

            await _handler.Handle(new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.Equal(30m, capturedContext.LimiteInferior);
            Assert.Equal(70m, capturedContext.LimiteSuperior);
            Assert.Equal("Pivot Sul", capturedContext.PivotName);
        }

        [Fact]
        public async Task Handle_WithSensorsAndReadings_ShouldPopulateSensorReadingsContext()
        {
            var sensor = new Sensor { Id = 10, PivoId = 1, UserId = 42, Quadrante = 1, Code = "S10" };
            var reading = new ReadSensor { Id = 100, SensorId = 10, UserId = 42, Humidity = 55.5m, Date = DateTime.UtcNow.AddHours(-1) };

            MongoMockHelper.SetupFind(_mockPivots, DefaultPivot);
            MongoMockHelper.SetupFind(_mockUsers, DefaultUser);
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor> { sensor });
            MongoMockHelper.SetupFindList(_mockReadSensors, new List<ReadSensor> { reading });
            MongoMockHelper.SetupFindList(_mockAnomalies, new List<SensorAnomaly>());

            PivotAIContext? capturedContext = null;
            _mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()))
                .Callback<PivotAIContext, CancellationToken>((ctx, _) => capturedContext = ctx)
                .ReturnsAsync("ok");

            var result = await _handler.Handle(new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(capturedContext);
            Assert.Single(capturedContext.SensorReadings);
            Assert.Equal("S10", capturedContext.SensorReadings[0].SensorCode);
            Assert.Single(capturedContext.SensorReadings[0].Readings);
            Assert.Equal(55.5m, capturedContext.SensorReadings[0].Readings[0].Value);
        }

        [Fact]
        public async Task Handle_WithAnomalies_ShouldPopulateAnomaliesInContext()
        {
            var sensor = new Sensor { Id = 10, PivoId = 1, UserId = 42, Quadrante = 1, Code = "S10" };
            var anomaly = new SensorAnomaly
            {
                Id = 1, SensorId = 10, PivotId = 1, UserId = 42,
                Value = 15m, ExpectedMin = 30m, ExpectedMax = 70m,
                Date = DateTime.UtcNow.AddMinutes(-30), Acknowledged = false
            };

            MongoMockHelper.SetupFind(_mockPivots, DefaultPivot);
            MongoMockHelper.SetupFind(_mockUsers, DefaultUser);
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor> { sensor });
            MongoMockHelper.SetupFindList(_mockReadSensors, new List<ReadSensor>());
            MongoMockHelper.SetupFindList(_mockAnomalies, new List<SensorAnomaly> { anomaly });

            PivotAIContext? capturedContext = null;
            _mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()))
                .Callback<PivotAIContext, CancellationToken>((ctx, _) => capturedContext = ctx)
                .ReturnsAsync("ok");

            var result = await _handler.Handle(new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(capturedContext);
            Assert.Single(capturedContext.RecentAnomalies);
            Assert.Equal(10, capturedContext.RecentAnomalies[0].SensorId);
            Assert.Equal(15m, capturedContext.RecentAnomalies[0].Value);
            Assert.Equal(30m, capturedContext.RecentAnomalies[0].ExpectedMin);
            Assert.Equal(70m, capturedContext.RecentAnomalies[0].ExpectedMax);
        }

        [Fact]
        public async Task Handle_PivotWithLocationAndForecastAvailable_ShouldIncludeForecastSummary()
        {
            var pivotWithLocation = new Pivot
            {
                Id = 1, UserId = 42, Name = "Pivot Norte",
                LimiteInferior = 30m, LimiteSuperior = 70m,
                Latitude = -27.59, Longitude = -48.55
            };
            var forecast = new WeatherForecast
            {
                IsAvailable = true,
                Source = "OpenMeteo",
                TotalPrecipitationMm = 12.5,
                DailyForecasts = new[]
                {
                    new DailyForecast(DateOnly.FromDateTime(DateTime.UtcNow), 6.0, 80.0),
                    new DailyForecast(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 6.5, 60.0)
                }
            };

            MongoMockHelper.SetupFind(_mockPivots, pivotWithLocation);
            MongoMockHelper.SetupFind(_mockUsers, DefaultUser);
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor>());
            MongoMockHelper.SetupFindList(_mockAnomalies, new List<SensorAnomaly>());
            _mockForecast.Setup(f => f.GetForecastAsync(-27.59, -48.55, 7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(forecast);

            PivotAIContext? capturedContext = null;
            _mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()))
                .Callback<PivotAIContext, CancellationToken>((ctx, _) => capturedContext = ctx)
                .ReturnsAsync("ok");

            var result = await _handler.Handle(new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(capturedContext);
            Assert.NotNull(capturedContext.ForecastSummary);
            Assert.Contains("12", capturedContext.ForecastSummary);
        }

        [Fact]
        public async Task Handle_ForecastServiceThrows_ShouldContinueAndReturnInsights()
        {
            var pivotWithLocation = new Pivot
            {
                Id = 1, UserId = 42, Name = "Pivot Norte",
                LimiteInferior = 30m, LimiteSuperior = 70m,
                Latitude = -27.59, Longitude = -48.55
            };
            MongoMockHelper.SetupFind(_mockPivots, pivotWithLocation);
            MongoMockHelper.SetupFind(_mockUsers, DefaultUser);
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor>());
            MongoMockHelper.SetupFindList(_mockAnomalies, new List<SensorAnomaly>());
            _mockForecast.Setup(f => f.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Network error"));

            var result = await _handler.Handle(new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_GlobalAiSettingsHaveNoKey_ShouldNotifyAndReturnNull()
        {
            MongoMockHelper.SetupFind(_mockPivots, DefaultPivot);
            MongoMockHelper.SetupFind(_mockUsers, DefaultUser);
            MongoMockHelper.SetupFind<PlatformAiSettings>(_mockPlatformAiSettings, null);

            var result = await _handler.Handle(new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(_notifier.HasNotification());
            _mockAI.Verify(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_PivotLimitsNull_ShouldFallbackToUserLimits()
        {
            var pivotNoLimits = new Pivot { Id = 1, UserId = 42, Name = "P", LimiteInferior = null, LimiteSuperior = null };
            var userWithLimits = new User { Id = 42, Name = "F", Email = "f@f.com", Password = "x", LimiteInferior = 20m, LimiteSuperior = 80m };
            MongoMockHelper.SetupFind(_mockPivots, pivotNoLimits);
            MongoMockHelper.SetupFind(_mockUsers, userWithLimits);
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor>());
            MongoMockHelper.SetupFindList(_mockAnomalies, new List<SensorAnomaly>());

            PivotAIContext? capturedContext = null;
            _mockAI.Setup(ai => ai.GetInsightsAsync(It.IsAny<PivotAIContext>(), It.IsAny<CancellationToken>()))
                .Callback<PivotAIContext, CancellationToken>((ctx, _) => capturedContext = ctx)
                .ReturnsAsync("ok");

            await _handler.Handle(new GetPivotAIInsightsRequest { PivotId = 1, UserId = 42 }, CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.Equal(20m, capturedContext.LimiteInferior);
            Assert.Equal(80m, capturedContext.LimiteSuperior);
        }
    }
}
