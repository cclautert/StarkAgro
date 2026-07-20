using StarkAgroAPI.Configuration;
using StarkAgroAPI.Domain.Commands.Requests.Anomalies;
using StarkAgroAPI.Domain.Handlers.Anomalies;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services;
using StarkAgroAPI.Tests.Helpers;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Anomalies
{
    public class DetectSensorAnomalyHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<IMongoCollection<Pivot>> _mockPivots;
        private readonly Mock<IMongoCollection<User>> _mockUsers;
        private readonly Mock<ISensorAnomalyService> _mockAnomalyService;
        private readonly Mock<IPushNotificationService> _mockPushService;
        private readonly Mock<IAgricultureWeatherService> _mockWeatherService;
        private readonly WeatherForecastSettings _weatherSettings;
        private readonly DetectSensorAnomalyHandler _handler;

        public DetectSensorAnomalyHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockPivots = new Mock<IMongoCollection<Pivot>>();
            _mockUsers = new Mock<IMongoCollection<User>>();
            _mockAnomalyService = new Mock<ISensorAnomalyService>();
            _mockPushService = new Mock<IPushNotificationService>();
            _mockWeatherService = new Mock<IAgricultureWeatherService>();
            _weatherSettings = new WeatherForecastSettings();

            _mockDbContext.Setup(db => db.Sensors).Returns(_mockSensors.Object);
            _mockDbContext.Setup(db => db.ReadSensors).Returns(_mockReadSensors.Object);
            _mockDbContext.Setup(db => db.Pivots).Returns(_mockPivots.Object);
            _mockDbContext.Setup(db => db.Users).Returns(_mockUsers.Object);
            MongoMockHelper.SetupFind<Pivot>(_mockPivots, null);
            MongoMockHelper.SetupFind<User>(_mockUsers, null);

            _mockAnomalyService
                .Setup(s => s.DetectAndSaveAsync(
                    It.IsAny<ReadSensor>(),
                    It.IsAny<int>(),
                    It.IsAny<IReadOnlyList<ReadSensor>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockPushService
                .Setup(p => p.SendAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockWeatherService
                .Setup(w => w.GetRecentPrecipitationAsync(
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((double?)null);

            _handler = new DetectSensorAnomalyHandler(
                _mockDbContext.Object,
                _mockAnomalyService.Object,
                _mockPushService.Object,
                _mockWeatherService.Object,
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(_weatherSettings),
                Mock.Of<ILogger<DetectSensorAnomalyHandler>>());
        }

        private static List<ReadSensor> BuildBaselineReadings(int count, decimal baseValue = 50m)
        {
            return Enumerable.Range(0, count).Select(i => new ReadSensor
            {
                Id = 1000 + i, SensorId = 1, UserId = 10, Humidity = baseValue, Date = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();
        }

        private void SetupRain(double? recentMm) =>
            _mockWeatherService
                .Setup(w => w.GetRecentPrecipitationAsync(
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recentMm);

        private void VerifyDetectionCalled(Times times) =>
            _mockAnomalyService.Verify(s => s.DetectAndSaveAsync(
                It.IsAny<ReadSensor>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<ReadSensor>>(), It.IsAny<CancellationToken>()), times);

        [Fact]
        public async Task Handle_SensorNotFound_ShouldReturnWithoutCallingService()
        {
            MongoMockHelper.SetupFind<Sensor>(_mockSensors, null);

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 99m
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            VerifyDetectionCalled(Times.Never());
        }

        [Fact]
        public async Task Handle_ValidSensor_ShouldFetchBaselineAndCallService()
        {
            var sensor = new Sensor { Id = 1, PivoId = 5, UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 50m
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            _mockAnomalyService.Verify(s => s.DetectAndSaveAsync(
                It.Is<ReadSensor>(r => r.Id == 999 && r.SensorId == 1 && r.UserId == 10 && r.Humidity == 50m),
                5,
                It.IsAny<IReadOnlyList<ReadSensor>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidSensor_ShouldPassPivotIdToService()
        {
            var sensor = new Sensor { Id = 1, PivoId = 42, UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(5));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 1, SensorId = 1, UserId = 10, Humidity = 50m
            };

            await _handler.Handle(request, CancellationToken.None);

            _mockAnomalyService.Verify(s => s.DetectAndSaveAsync(
                It.IsAny<ReadSensor>(),
                42,
                It.IsAny<IReadOnlyList<ReadSensor>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── Stale baseline fallback (self-lock fix) ─────────────────────────────

        [Fact]
        public async Task Handle_StaleNonAnomalousBaseline_FallsBackToRawReadings()
        {
            var sensor = new Sensor { Id = 1, PivoId = 5, UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            var staleBaseline = Enumerable.Range(0, 20).Select(i => new ReadSensor
            {
                Id = 2000 + i, SensorId = 1, UserId = 10, Humidity = 50m,
                Date = DateTime.UtcNow.AddHours(-30).AddMinutes(-i)
            }).ToList();
            var rawFallback = Enumerable.Range(0, 20).Select(i => new ReadSensor
            {
                Id = 3000 + i, SensorId = 1, UserId = 10, Humidity = 90m,
                Date = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();

            _mockReadSensors.SetupSequence(c => c.FindAsync(
                    It.IsAny<FilterDefinition<ReadSensor>>(),
                    It.IsAny<FindOptions<ReadSensor, ReadSensor>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(staleBaseline).Object)
                .ReturnsAsync(MongoMockHelper.CreateMockCursor(rawFallback).Object);

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 91m
            };

            await _handler.Handle(request, CancellationToken.None);

            _mockAnomalyService.Verify(s => s.DetectAndSaveAsync(
                It.IsAny<ReadSensor>(),
                5,
                It.Is<IReadOnlyList<ReadSensor>>(list => list.Count == rawFallback.Count && list[0].Id == rawFallback[0].Id),
                It.IsAny<CancellationToken>()), Times.Once);

            _mockReadSensors.Verify(c => c.FindAsync(
                It.IsAny<FilterDefinition<ReadSensor>>(),
                It.IsAny<FindOptions<ReadSensor, ReadSensor>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Handle_FreshNonAnomalousBaseline_DoesNotFallBack()
        {
            var sensor = new Sensor { Id = 1, PivoId = 5, UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 50m
            };

            await _handler.Handle(request, CancellationToken.None);

            _mockReadSensors.Verify(c => c.FindAsync(
                It.IsAny<FilterDefinition<ReadSensor>>(),
                It.IsAny<FindOptions<ReadSensor, ReadSensor>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── Rain suppression of high-humidity anomalies ─────────────────────────

        private void SetupHighReadingScenario(Pivot? pivot, User? user = null)
        {
            var sensor = new Sensor { Id = 1, PivoId = 5, UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20, 50m));
            MongoMockHelper.SetupFind(_mockPivots, pivot);
            MongoMockHelper.SetupFind(_mockUsers, user);
        }

        [Fact]
        public async Task Handle_HighReadingWithRecentRainAboveThreshold_SuppressesDetection()
        {
            SetupHighReadingScenario(new Pivot { Id = 5, Latitude = -29.5, Longitude = -51.2 });
            SetupRain(20.0); // default threshold 5mm

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 99.9m
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            VerifyDetectionCalled(Times.Never());
            _mockPushService.Verify(p => p.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_HighReadingWithRainBelowThreshold_DetectsNormally()
        {
            SetupHighReadingScenario(new Pivot { Id = 5, Latitude = -29.5, Longitude = -51.2 });
            SetupRain(1.0);

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 99.9m
            };

            await _handler.Handle(request, CancellationToken.None);

            VerifyDetectionCalled(Times.Once());
        }

        [Fact]
        public async Task Handle_LowReadingDuringRain_IsNeverSuppressed()
        {
            SetupHighReadingScenario(new Pivot { Id = 5, Latitude = -29.5, Longitude = -51.2 });
            SetupRain(20.0);

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 10m
            };

            await _handler.Handle(request, CancellationToken.None);

            VerifyDetectionCalled(Times.Once());
            _mockWeatherService.Verify(w => w.GetRecentPrecipitationAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_PivotWithoutCoordinates_DetectsNormallyWithoutWeatherCall()
        {
            SetupHighReadingScenario(new Pivot { Id = 5, Latitude = null, Longitude = null });

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 99.9m
            };

            await _handler.Handle(request, CancellationToken.None);

            VerifyDetectionCalled(Times.Once());
            _mockWeatherService.Verify(w => w.GetRecentPrecipitationAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WeatherUnavailable_FailsOpenAndDetects()
        {
            SetupHighReadingScenario(new Pivot { Id = 5, Latitude = -29.5, Longitude = -51.2 });
            SetupRain(null);

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 99.9m
            };

            await _handler.Handle(request, CancellationToken.None);

            VerifyDetectionCalled(Times.Once());
        }

        [Fact]
        public async Task Handle_PivotRainThresholdTakesPrecedence_OverSettings()
        {
            // Pivot threshold 30mm > observed 20mm → not suppressed even though settings default (5mm) would suppress
            SetupHighReadingScenario(new Pivot { Id = 5, Latitude = -29.5, Longitude = -51.2, RainThresholdMm = 30.0 });
            SetupRain(20.0);

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999, SensorId = 1, UserId = 10, Humidity = 99.9m
            };

            await _handler.Handle(request, CancellationToken.None);

            VerifyDetectionCalled(Times.Once());
        }
    }
}
