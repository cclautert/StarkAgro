using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class EditSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_Sensor_And_Returns_Response()
        {
            // Arrange
            var sensor = new Sensor { Id = 10, Code = "OLD", Quadrante = 1, PivoId = 2 };
            var mockSet = new Mock<DbSet<Sensor>>();
            mockSet.Setup(m => m.FindAsync(10)).ReturnsAsync(sensor);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var handler = new EditSensorHandler(mockContext.Object);
            var request = new EditSensorRequest
            {
                Id = 10,
                Code = "NEW",
                Quadrante = 5,
                Pivot = new Pivot { Id = 7 }
            };

            // Act
            var result = await handler.Handle(request, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result.Id);
            Assert.Equal("NEW", sensor.Code);
            Assert.Equal(5, sensor.Quadrante);
            Assert.Equal(7, sensor.PivoId);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Throws_If_Sensor_Not_Found()
        {
            // Arrange
            var mockSet = new Mock<DbSet<Sensor>>();
            mockSet.Setup(m => m.FindAsync(99)).ReturnsAsync((Sensor)null);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSet.Object);

            var handler = new EditSensorHandler(mockContext.Object);
            var request = new EditSensorRequest
            {
                Id = 99,
                Code = "ANY",
                Quadrante = 1,
                Pivot = new Pivot { Id = 1 }
            };

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(request, default));
        }
    }
}