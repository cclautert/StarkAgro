using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services
{
    public class SensorAnomalyServiceTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<IMongoCollection<SensorAnomaly>> _mockSensorAnomalies;
        private readonly SensorAnomalyService _service;

        public SensorAnomalyServiceTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockSensorAnomalies = new Mock<IMongoCollection<SensorAnomaly>>();
            var mockLogger = new Mock<ILogger<SensorAnomalyService>>();

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

            _service = new SensorAnomalyService(_mockDbContext.Object, mockLogger.Object);
        }

        private static ReadSensor MakeReading(decimal value, int id = 999, int sensorId = 1, int userId = 10) =>
            new ReadSensor { Id = id, SensorId = sensorId, UserId = userId, Humidity = value, Date = DateTime.UtcNow };

        private static List<ReadSensor> BuildStableBaseline(int count, decimal baseValue = 50m, decimal spread = 2m)
        {
            var rng = new Random(42);
            return Enumerable.Range(0, count).Select(i => new ReadSensor
            {
                Id = 1000 + i, SensorId = 1, UserId = 10,
                Humidity = baseValue + (decimal)(rng.NextDouble() * (double)(spread * 2) - (double)spread),
                Date = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();
        }

        // Scenario 1: Normal reading within ±2.5 stddev — no anomaly saved
        [Fact]
        public async Task DetectAndSave_NormalReading_ShouldReturnFalseAndNotPersist()
        {
            var baseline = BuildStableBaseline(20, baseValue: 50m, spread: 2m);
            var reading = MakeReading(51m);

            var result = await _service.DetectAndSaveAsync(reading, pivotId: 1, baseline, CancellationToken.None);

            Assert.False(result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.IsAny<SensorAnomaly>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockReadSensors.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<ReadSensor>>(), It.IsAny<UpdateDefinition<ReadSensor>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Scenario 2: Spike anomaly — value much higher than mean
        [Fact]
        public async Task DetectAndSave_SpikeValue_ShouldReturnTrueAndSaveAnomaly()
        {
            var baseline = BuildStableBaseline(20, baseValue: 50m, spread: 2m);
            var reading = MakeReading(150m);

            var result = await _service.DetectAndSaveAsync(reading, pivotId: 1, baseline, CancellationToken.None);

            Assert.True(result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.Is<SensorAnomaly>(a =>
                    a.SensorId == 1 &&
                    a.PivotId == 1 &&
                    a.UserId == 10 &&
                    a.Value == 150m &&
                    a.ExpectedMin < a.ExpectedMax &&
                    !a.Acknowledged),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Scenario 3: Sudden drop — value much lower than mean
        [Fact]
        public async Task DetectAndSave_SuddenDrop_ShouldReturnTrueAndSaveAnomaly()
        {
            var baseline = BuildStableBaseline(20, baseValue: 50m, spread: 2m);
            var reading = MakeReading(-50m);

            var result = await _service.DetectAndSaveAsync(reading, pivotId: 1, baseline, CancellationToken.None);

            Assert.True(result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.Is<SensorAnomaly>(a => a.Value == -50m && !a.Acknowledged),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DetectAndSave_InsufficientBaseline_ShouldReturnFalse()
        {
            var baseline = BuildStableBaseline(5); // fewer than MinSamples (10)
            var reading = MakeReading(9999m);

            var result = await _service.DetectAndSaveAsync(reading, pivotId: 1, baseline, CancellationToken.None);

            Assert.False(result);
            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.IsAny<SensorAnomaly>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DetectAndSave_FlatDistribution_ShouldReturnFalse()
        {
            var baseline = Enumerable.Range(0, 20).Select(i => new ReadSensor
            {
                Id = 1000 + i, SensorId = 1, UserId = 10, Humidity = 50m, Date = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();

            var result = await _service.DetectAndSaveAsync(MakeReading(9999m), pivotId: 1, baseline, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task DetectAndSave_AnomalyDetected_ShouldMarkReadSensorAsAnomaly()
        {
            var baseline = BuildStableBaseline(20, baseValue: 50m, spread: 2m);

            await _service.DetectAndSaveAsync(MakeReading(150m), pivotId: 1, baseline, CancellationToken.None);

            _mockReadSensors.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<ReadSensor>>(),
                It.IsAny<UpdateDefinition<ReadSensor>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DetectAndSave_AnomalyDetected_ShouldSetPivotIdOnRecord()
        {
            var baseline = BuildStableBaseline(20, baseValue: 50m, spread: 2m);

            await _service.DetectAndSaveAsync(MakeReading(150m), pivotId: 7, baseline, CancellationToken.None);

            _mockSensorAnomalies.Verify(c => c.InsertOneAsync(
                It.Is<SensorAnomaly>(a => a.PivotId == 7),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
