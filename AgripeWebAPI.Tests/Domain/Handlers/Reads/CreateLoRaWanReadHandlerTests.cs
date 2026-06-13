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
    public class CreateLoRaWanReadHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<ILogger<CreateLoRaWanReadHandler>> _mockLogger;
        private readonly CreateLoRaWanReadHandler _handler;

        public CreateLoRaWanReadHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockLogger = new Mock<ILogger<CreateLoRaWanReadHandler>>();

            _mockDbContext.Setup(db => db.Sensors).Returns(_mockSensors.Object);
            _mockDbContext.Setup(db => db.ReadSensors).Returns(_mockReadSensors.Object);
            _mockDbContext.Setup(db => db.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(99);
            _mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _handler = new CreateLoRaWanReadHandler(_mockDbContext.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CreateLoRaWanReadHandler(null!, _mockLogger.Object));
        }

        [Fact]
        public async Task Handle_SensorNotFound_ReturnsNull()
        {
            MongoMockHelper.SetupFind<Sensor>(_mockSensors, null);

            var result = await _handler.Handle(
                new CreateLoRaWanReadRequest { Code = "NOTFOUND", Humidity = 70m },
                CancellationToken.None);

            Assert.Null(result);
            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_SensorFound_PersistsReadWithAllThreeMetrics()
        {
            var sensor = new Sensor { Id = 7, Code = "A84041691D5F1794", UserId = 42 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            ReadSensor? inserted = null;
            _mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<ReadSensor, InsertOneOptions?, CancellationToken>((r, _, _) => inserted = r)
                .Returns(Task.CompletedTask);

            var request = new CreateLoRaWanReadRequest
            {
                Code = "A84041691D5F1794",
                Humidity = 75.0m,
                Temperature = 22.7m,
                BatteryVoltage = 3.582m,
                ReadAt = new DateTime(2026, 6, 11, 23, 29, 2, DateTimeKind.Utc),
                Fcnt = 126
            };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(99, result!.Id);
            Assert.Equal(7, result.SensorId);
            Assert.Equal(42, result.UserId);

            Assert.NotNull(inserted);
            Assert.Equal(75.0m, inserted!.Humidity);
            Assert.Equal(22.7m, inserted.Temperature);
            Assert.Equal(3.582m, inserted.BatteryVoltage);
            Assert.Equal(0m, inserted.Value);
            Assert.Equal(new DateTime(2026, 6, 11, 23, 29, 2, DateTimeKind.Utc), inserted.Date.ToUniversalTime());
            Assert.Equal("A84041691D5F1794:126", inserted.IdempotencyKey);
        }

        [Fact]
        public async Task Handle_NullReadAt_UsesUtcNow()
        {
            var sensor = new Sensor { Id = 1, Code = "DEVEUI", UserId = 1 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            ReadSensor? inserted = null;
            _mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<ReadSensor, InsertOneOptions?, CancellationToken>((r, _, _) => inserted = r)
                .Returns(Task.CompletedTask);

            var before = DateTime.UtcNow;
            await _handler.Handle(
                new CreateLoRaWanReadRequest { Code = "DEVEUI", Humidity = 60m },
                CancellationToken.None);
            var after = DateTime.UtcNow;

            Assert.NotNull(inserted);
            Assert.InRange(inserted!.Date.ToUniversalTime(), before, after);
            Assert.NotNull(inserted.IdempotencyKey);
            Assert.True(Guid.TryParse(inserted.IdempotencyKey, out _));
        }

        [Fact]
        public async Task Handle_PartialMetrics_PersistsNullsForMissingFields()
        {
            var sensor = new Sensor { Id = 3, Code = "DEVEUI2", UserId = 5 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            ReadSensor? inserted = null;
            _mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<ReadSensor, InsertOneOptions?, CancellationToken>((r, _, _) => inserted = r)
                .Returns(Task.CompletedTask);

            await _handler.Handle(
                new CreateLoRaWanReadRequest { Code = "DEVEUI2", Humidity = 65m },
                CancellationToken.None);

            Assert.NotNull(inserted);
            Assert.Equal(65m, inserted!.Humidity);
            Assert.Null(inserted.Temperature);
            Assert.Null(inserted.BatteryVoltage);
            Assert.NotNull(inserted.IdempotencyKey);
            Assert.True(Guid.TryParse(inserted.IdempotencyKey, out _));
        }
    }
}
