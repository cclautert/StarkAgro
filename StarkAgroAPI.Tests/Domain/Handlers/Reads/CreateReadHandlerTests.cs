using StarkAgroAPI.Domain.Commands.Requests.Reads;
using StarkAgroAPI.Domain.Commands.Responses.Reads;
using StarkAgroAPI.Domain.Handlers.Reads;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Reads
{
    public class CreateReadHandlerTests
    {
        private static Mock<ICurrentUserContext> AuthenticatedUser(int userId)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(c => c.UserId).Returns(userId);
            mock.Setup(c => c.IsAuthenticated).Returns(true);
            return mock;
        }

        private static Mock<ICurrentUserContext> AnonymousUser()
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(c => c.UserId).Returns((int?)null);
            mock.Setup(c => c.IsAuthenticated).Returns(false);
            return mock;
        }

        [Fact]
        public async Task Handle_Should_Add_ReadSensor_When_Sensor_Exists()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 1 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>())).ReturnsAsync(42);
            mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 512 };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<CreateReadResponse>(result);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs => rs.SensorId == 1),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Throw_When_Sensor_Does_Not_Exist()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupFind<Sensor>(mockSensors, null);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "NOT-FOUND", Value = 10m };

            await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(request, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_StoresRawValue()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 1 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>())).ReturnsAsync(100);
            mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 512 };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs => rs.Humidity == 512),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_UsesUserIdFromSensor()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 5 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>())).ReturnsAsync(101);
            mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(5).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 512 };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs => rs.UserId == 5),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Throw_UnauthorizedAccessException_When_Unauthenticated()
        {
            var mockDbContext = new Mock<agpDBContext>();

            var handler = new CreateReadHandler(mockDbContext.Object, AnonymousUser().Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 10m };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(request, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_Should_Throw_UnauthorizedAccessException_When_Sensor_Belongs_To_Different_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 7 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(99).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 10m };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(request, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithIsEdgeAnomalyTrue_ShouldPersistIsEdgeAnomalyAndEdgeDetectedAt()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 1 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>())).ReturnsAsync(10);
            mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var before = DateTime.UtcNow;
            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 100m, IsEdgeAnomaly = true };

            await handler.Handle(request, CancellationToken.None);

            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs =>
                    rs.IsEdgeAnomaly == true &&
                    rs.EdgeDetectedAt.HasValue &&
                    rs.EdgeDetectedAt.Value >= before),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithIsEdgeAnomalyFalse_ShouldNotSetEdgeDetectedAt()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 1 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>())).ReturnsAsync(11);
            mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 100m, IsEdgeAnomaly = false };

            await handler.Handle(request, CancellationToken.None);

            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs =>
                    rs.IsEdgeAnomaly == false &&
                    rs.EdgeDetectedAt == null),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithExistingIdempotencyKey_ReturnsExistingReadWithoutInserting()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 1 };
            var existingRead = new ReadSensor { Id = 99, SensorId = 1, UserId = 1, Humidity = 42m, IdempotencyKey = "device-abc-1234567890" };

            MongoMockHelper.SetupFind(mockSensors, sensor);
            MongoMockHelper.SetupFind(mockReadSensors, existingRead);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 100m, IdempotencyKey = "device-abc-1234567890" };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.Equal(99, result.Id);
            Assert.Equal(1, result.SensorId);
            Assert.Equal("device-abc-1234567890", result.IdempotencyKey);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WithNewIdempotencyKey_InsertsAndReturnsWithKey()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 1 };

            MongoMockHelper.SetupFind(mockSensors, sensor);
            MongoMockHelper.SetupFind<ReadSensor>(mockReadSensors, null);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>())).ReturnsAsync(50);
            mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 100m, IdempotencyKey = "device-abc-9999999999" };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.Equal(50, result.Id);
            Assert.Equal("device-abc-9999999999", result.IdempotencyKey);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs => rs.IdempotencyKey == "device-abc-9999999999"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithoutIdempotencyKey_InsertsWithoutIdempotencyCheck()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            var sensor = new Sensor { Id = 1, Code = "SENSOR-1", UserId = 1 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("ReadSensor", It.IsAny<CancellationToken>())).ReturnsAsync(60);
            mockReadSensors.Setup(c => c.InsertOneAsync(It.IsAny<ReadSensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreateReadHandler(mockDbContext.Object, AuthenticatedUser(1).Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 55m };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.Equal(60, result.Id);
            Assert.Null(result.IdempotencyKey);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs => rs.IdempotencyKey == null),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
