using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Domain.Handlers.Anomalies;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Anomalies
{
    public class DetectSensorAnomalyHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<IMongoCollection<SensorAnomaly>> _mockSensorAnomalies;
        private readonly Mock<ILogger<DetectSensorAnomalyHandler>> _mockLogger;
        private readonly DetectSensorAnomalyHandler _handler;

        public DetectSensorAnomalyHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockSensorAnomalies = new Mock<IMongoCollection<SensorAnomaly>>();
            _mockLogger = new Mock<ILogger<DetectSensorAnomalyHandler>>();

            _mockDbContext.Setup(db => db.ReadSensors).Returns(_mockReadSensors.Object);
            _mockDbContext.Setup(db => db.SensorAnomalies).Returns(_mockSensorAnomalies.Object);
            _mockDbContext.Setup(db => db.GetNextIdAsync("SensorAnomaly", It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            _mockReadSensors
                .Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<ReadSensor>>(),
                    It.IsAny<UpdateDefinition<ReadSensor>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            _mockSensorAnomalies
                .Setup(c => c.InsertOneAsync(It.IsAny<SensorAnomaly>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _handler = new DetectSensorAnomalyHandler(_mockDbContext.Object, _mockLogger.Object);
        }

        private static List<ReadSensor> BuildBaselineReadings(int count, decimal baseValue = 50m, decimal spread = 5m)
        {
            var readings = new List<ReadSensor>();
            var rng = new Random(42);
            for (int i = 0; i < count; i++)
            {
                readings.Add(new ReadSensor
                {
                    Id = 1000 + i,
                    SensorId = 1,
                    UserId = 10,
                    Value = baseValue + (decimal)(rng.NextDouble() * (double)spread * 2 - (double)spread),
                    Date = DateTime.UtcNow.AddMinutes(-i),
                    IsAnomaly = false
                });
            }
            return readings;
        }

        [Fact]
        public async Task Handle_InsufficientBaseline_ShouldReturnWithoutDetecting()
        {
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(5));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999,
                SensorId = 1,
                UserId = 10,
                Value = 999m
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.IsAny<SensorAnomaly>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ValueWithinRange_ShouldNotSaveAnomaly()
        {
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20, baseValue: 50m, spread: 2m));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999,
                SensorId = 1,
                UserId = 10,
                Value = 51m
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.IsAny<SensorAnomaly>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ValueFarOutsideRange_ShouldSaveAnomaly()
        {
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20, baseValue: 50m, spread: 2m));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999,
                SensorId = 1,
                UserId = 10,
                Value = 150m
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.Is<SensorAnomaly>(a =>
                    a.SensorId == 1 &&
                    a.UserId == 10 &&
                    a.ReadSensorId == 999 &&
                    a.Value == 150m &&
                    !a.Acknowledged),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_AnomalyDetected_ShouldMarkReadSensorAsAnomaly()
        {
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20, baseValue: 50m, spread: 2m));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999,
                SensorId = 1,
                UserId = 10,
                Value = 150m
            };

            await _handler.Handle(request, CancellationToken.None);

            _mockReadSensors.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<ReadSensor>>(),
                It.IsAny<UpdateDefinition<ReadSensor>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_AnomalyDetected_ShouldCallGetNextIdAsync()
        {
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20, baseValue: 50m, spread: 2m));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999,
                SensorId = 1,
                UserId = 10,
                Value = 150m
            };

            await _handler.Handle(request, CancellationToken.None);

            _mockDbContext.Verify(db => db.GetNextIdAsync("SensorAnomaly", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_AnomalyDetected_ShouldSetExpectedRangeBounds()
        {
            MongoMockHelper.SetupFindList(_mockReadSensors, BuildBaselineReadings(20, baseValue: 50m, spread: 2m));

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999,
                SensorId = 1,
                UserId = 10,
                Value = 150m
            };

            await _handler.Handle(request, CancellationToken.None);

            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.Is<SensorAnomaly>(a => a.ExpectedMin < a.ExpectedMax),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_FlatDistribution_ShouldSkipDetection()
        {
            var flatReadings = Enumerable.Range(0, 20).Select(i => new ReadSensor
            {
                Id = 1000 + i, SensorId = 1, UserId = 10, Value = 50m, Date = DateTime.UtcNow.AddMinutes(-i), IsAnomaly = false
            }).ToList();

            MongoMockHelper.SetupFindList(_mockReadSensors, flatReadings);

            var request = new DetectSensorAnomalyRequest
            {
                ReadSensorId = 999,
                SensorId = 1,
                UserId = 10,
                Value = 9999m
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Equal(Unit.Value, result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.IsAny<SensorAnomaly>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
