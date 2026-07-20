using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using StarkAgroAPI.Domain.Handlers.Sensors;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Sensors
{
    public class EditSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_Sensor_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            var sensor = new Sensor { Id = 10, Code = "OLD", Quadrante = 1, PivoId = 2 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockSensors.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Sensor>>(), It.IsAny<Sensor>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new EditSensorHandler(mockDbContext.Object);
            var request = new EditSensorRequest
            {
                Id = 10,
                Code = "NEW",
                Quadrante = 5,
                Pivot = new Pivot { Id = 7 }
            };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(10, result.Id);
            Assert.Equal("NEW", sensor.Code);
            Assert.Equal(5, sensor.Quadrante);
            Assert.Equal(7, sensor.PivoId);
        }

        [Fact]
        public async Task Handle_Throws_If_Sensor_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupFind<Sensor>(mockSensors, null);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new EditSensorHandler(mockDbContext.Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new EditSensorRequest { Id = 99, Code = "ANY", Quadrante = 1, Pivot = new Pivot { Id = 1 } }, default));
        }

        [Fact]
        public async Task Handle_NullPivot_ThrowsArgumentNullException()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            var sensor = new Sensor { Id = 10, Code = "OLD", Quadrante = 1, PivoId = 2 };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new EditSensorHandler(mockDbContext.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Handle(new EditSensorRequest { Id = 10, Code = "NEW", Quadrante = 1, Pivot = null }, default));
        }
    }
}
