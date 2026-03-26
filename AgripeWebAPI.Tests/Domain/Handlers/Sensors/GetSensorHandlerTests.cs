using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class GetSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Sensor_When_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var sensor = new Sensor { Id = 123, PivoId = 2, UserId = 3, Quadrante = 4, Code = "SENSOR-123" };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            MongoMockHelper.SetupFind(mockPivots, new Pivot { Id = 2, Name = "Pivot2" });
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetSensorHandler(mockDbContext.Object);
            var result = await handler.Handle(new GetSensorRequest { Id = 123 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(123, result!.Id);
            Assert.Equal(2, result.Pivot.Id);
            Assert.Equal(4, result.Quadrante);
            Assert.Equal("SENSOR-123", result.Code);
        }

        [Fact]
        public async Task Handle_Returns_Null_When_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupFind<Sensor>(mockSensors, null);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetSensorHandler(mockDbContext.Object);
            var result = await handler.Handle(new GetSensorRequest { Id = 0 }, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public void Constructor_Throws_If_DbContext_Is_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new GetSensorHandler(null!));
        }
    }
}
