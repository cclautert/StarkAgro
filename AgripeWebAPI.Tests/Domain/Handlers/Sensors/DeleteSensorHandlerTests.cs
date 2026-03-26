using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class DeleteSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Deletes_Sensor_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            MongoMockHelper.SetupDeleteOne(mockSensors, 1);
            MongoMockHelper.SetupDeleteMany(mockReadSensors);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);

            var handler = new DeleteSensorHandler(mockDbContext.Object);
            var result = await handler.Handle(new DeleteSensorRequest { Id = 5 }, default);

            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Handle_Throws_When_Sensor_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupDeleteOne(mockSensors, 0);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new DeleteSensorHandler(mockDbContext.Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new DeleteSensorRequest { Id = 999 }, default));
        }
    }
}
