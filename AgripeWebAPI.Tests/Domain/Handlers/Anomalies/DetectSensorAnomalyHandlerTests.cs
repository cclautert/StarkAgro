using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Domain.Handlers.Anomalies;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services;
using AgripeWebAPI.Tests.Helpers;
using MediatR;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Anomalies
{
    public class DetectSensorAnomalyHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<ISensorAnomalyService> _mockAnomalyService;
        private readonly Mock<IPushNotificationService> _mockPushService;
        private readonly DetectSensorAnomalyHandler _handler;

        public DetectSensorAnomalyHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockAnomalyService = new Mock<ISensorAnomalyService>();
            _mockPushService = new Mock<IPushNotificationService>();

            _mockDbContext.Setup(db => db.Sensors).Returns(_mockSensors.Object);
            _mockDbContext.Setup(db => db.ReadSensors).Returns(_mockReadSensors.Object);

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

            _handler = new DetectSensorAnomalyHandler(_mockDbContext.Object, _mockAnomalyService.Object, _mockPushService.Object);
        }

        private static List<ReadSensor> BuildBaselineReadings(int count, decimal baseValue = 50m)
        {
            return Enumerable.Range(0, count).Select(i => new ReadSensor
            {
                Id = 1000 + i, SensorId = 1, UserId = 10, Humidity = baseValue, Date = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();
        }

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
            _mockAnomalyService.Verify(s => s.DetectAndSaveAsync(
                It.IsAny<ReadSensor>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<ReadSensor>>(), It.IsAny<CancellationToken>()), Times.Never);
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
    }
}
