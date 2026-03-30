using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Domain.Handlers.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class CreateReadHandlerTests
    {
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

            var handler = new CreateReadHandler(mockDbContext.Object);
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

            var handler = new CreateReadHandler(mockDbContext.Object);
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

            var handler = new CreateReadHandler(mockDbContext.Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 512 };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs => rs.Value == 512),
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

            var handler = new CreateReadHandler(mockDbContext.Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 512 };

            var result = await handler.Handle(request, CancellationToken.None);

            Assert.NotNull(result);
            mockReadSensors.Verify(c => c.InsertOneAsync(
                It.Is<ReadSensor>(rs => rs.UserId == 5),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
