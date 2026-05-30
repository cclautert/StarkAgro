using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Handlers.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebWorker.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebWorker.Tests.Handlers
{
    public class CreateReadHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;
        private readonly Mock<ICurrentUserContext> _mockCurrentUser;
        private readonly CreateReadHandler _handler;

        public CreateReadHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();
            _mockCurrentUser = new Mock<ICurrentUserContext>();

            _mockDbContext.Setup(db => db.Sensors).Returns(_mockSensors.Object);
            _mockDbContext.Setup(db => db.ReadSensors).Returns(_mockReadSensors.Object);
            _mockDbContext.Setup(db => db.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(42);

            _handler = new CreateReadHandler(_mockDbContext.Object, _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_ShouldUseSensorUserId()
        {
            _mockCurrentUser.Setup(u => u.UserId).Returns(10);

            var sensor = new Sensor { Id = 1, Code = "SENS01", UserId = 10 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            _mockReadSensors
                .Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var request = new CreateReadRequest { Code = "SENS01", Value = 512 };
            await _handler.Handle(request, CancellationToken.None);

            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r => r.UserId == 10),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
        {
            _mockCurrentUser.Setup(u => u.UserId).Returns((int?)null);

            var request = new CreateReadRequest { Code = "SENS01", Value = 512 };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _handler.Handle(request, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_SensorNotFound_ShouldThrowKeyNotFoundException()
        {
            _mockCurrentUser.Setup(u => u.UserId).Returns(1);

            MongoMockHelper.SetupFind<Sensor>(_mockSensors, null);

            var request = new CreateReadRequest { Code = "INVALID", Value = 100 };

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _handler.Handle(request, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ShouldPersistRawSensorValue()
        {
            _mockCurrentUser.Setup(u => u.UserId).Returns(1);

            var sensor = new Sensor { Id = 1, Code = "SENS01", UserId = 1 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            _mockReadSensors
                .Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var request = new CreateReadRequest { Code = "SENS01", Value = 512 };
            await _handler.Handle(request, CancellationToken.None);

            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r => r.Value == 512),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCallGetNextIdAsync()
        {
            _mockCurrentUser.Setup(u => u.UserId).Returns(1);

            var sensor = new Sensor { Id = 1, Code = "SENS01", UserId = 1 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            _mockReadSensors
                .Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var request = new CreateReadRequest { Code = "SENS01", Value = 100 };
            await _handler.Handle(request, CancellationToken.None);

            _mockDbContext.Verify(db => db.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCallInsertOneAsync_WithCorrectFields()
        {
            _mockCurrentUser.Setup(u => u.UserId).Returns(3);

            var sensor = new Sensor { Id = 7, Code = "SENS07", UserId = 3 };
            MongoMockHelper.SetupFind(_mockSensors, sensor);

            _mockReadSensors
                .Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var request = new CreateReadRequest { Code = "SENS07", Value = 200 };
            await _handler.Handle(request, CancellationToken.None);

            _mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(r =>
                    r.SensorId == 7 &&
                    r.UserId == 3 &&
                    r.Id == 42 &&
                    r.Date <= DateTime.UtcNow &&
                    r.Date > DateTime.UtcNow.AddMinutes(-1)),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
