using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class DeleteSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Deletes_Sensor_And_Returns_Response()
        {
            // Arrange
            var sensor = new Sensor { Id = 5, Code = "DEL" };
            var sensors = new List<Sensor> { sensor }.AsQueryable();

            var mockSet = new Mock<DbSet<Sensor>>();
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.Provider).Returns(sensors.Provider);
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.Expression).Returns(sensors.Expression);
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.ElementType).Returns(sensors.ElementType);
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.GetEnumerator()).Returns(sensors.GetEnumerator());
            mockSet.Setup(m => m.Remove(It.IsAny<Sensor>())).Callback<Sensor>(s => { });

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChanges()).Returns(1);

            // You may need to implement the handler's logic to use Find or FirstOrDefault
            var handler = new DeleteSensorHandler(mockContext.Object);
            var request = new DeleteSensorRequest { Id = 5 };

            // Act
            var result = await handler.Handle(request, default);

            // Assert
            mockSet.Verify(m => m.Remove(It.Is<Sensor>(s => s.Id == 5)), Times.Once);
            mockContext.Verify(c => c.SaveChanges(), Times.Once);
            Assert.NotNull(result);
        }
    }
}