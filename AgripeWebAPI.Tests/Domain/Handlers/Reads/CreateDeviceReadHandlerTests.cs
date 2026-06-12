using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Handlers.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class CreateDeviceReadHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<ILogger<CreateDeviceReadHandler>> _mockLogger;
        private readonly CreateDeviceReadHandler _handler;

        public CreateDeviceReadHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockLogger = new Mock<ILogger<CreateDeviceReadHandler>>();

            _mockDbContext.Setup(db => db.Sensors).Returns(_mockSensors.Object);
            _mockDbContext.Setup(db => db.ReadSensors).Returns(_mockReadSensors.Object);
            _mockDbContext.Setup(db => db.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(42);
            _mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _handler = new CreateDeviceReadHandler(_mockDbContext.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Handle_PersistsRead_WithSensorUserId()
        {
            var sensor = new Sensor { Id = 5, Code = "ABC123_H", UserId = 99 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            var result = await _handler.Handle(
                new CreateDeviceReadRequest { Code = "ABC123_H", Value = 75.0m },
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(99, result!.UserId);
            Assert.Equal(5, result.SensorId);
            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r => r.UserId == 99 && r.SensorId == 5 && r.Value == 75.0m),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UsesProvidedReadAt_WhenSupplied()
        {
            var sensor = new Sensor { Id = 1, Code = "ABC123_T", UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            var readAt = new DateTime(2026, 6, 11, 23, 29, 2, DateTimeKind.Utc);
            await _handler.Handle(
                new CreateDeviceReadRequest { Code = "ABC123_T", Value = 22.7m, ReadAt = readAt },
                CancellationToken.None);

            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r => r.Date == readAt),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DefaultsToUtcNow_WhenReadAtNull()
        {
            var sensor = new Sensor { Id = 1, Code = "ABC123_H", UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            var before = DateTime.UtcNow.AddSeconds(-2);
            await _handler.Handle(
                new CreateDeviceReadRequest { Code = "ABC123_H", Value = 50m, ReadAt = null },
                CancellationToken.None);
            var after = DateTime.UtcNow.AddSeconds(2);

            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r => r.Date >= before && r.Date <= after),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ReturnsNull_WhenSensorNotFound()
        {
            MongoMockHelper.SetupFind<Sensor>(_mockSensors, null);

            var result = await _handler.Handle(
                new CreateDeviceReadRequest { Code = "UNKNOWN_H", Value = 10m },
                CancellationToken.None);

            Assert.Null(result);
            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_CallsGetNextIdAsync_BeforeInsert()
        {
            var sensor = new Sensor { Id = 3, Code = "ABC123_B", UserId = 7 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            await _handler.Handle(
                new CreateDeviceReadRequest { Code = "ABC123_B", Value = 3.58m },
                CancellationToken.None);

            _mockDbContext.Verify(db => db.GetNextIdAsync(nameof(ReadSensor), It.IsAny<CancellationToken>()), Times.Once);
            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r => r.Id == 42),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserIdAlwaysFromSensor_NotFromPayload()
        {
            var sensor = new Sensor { Id = 1, Code = "ABC123_H", UserId = 55 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            var result = await _handler.Handle(
                new CreateDeviceReadRequest { Code = "ABC123_H", Value = 10m },
                CancellationToken.None);

            Assert.Equal(55, result!.UserId);
            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r => r.UserId == 55),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
