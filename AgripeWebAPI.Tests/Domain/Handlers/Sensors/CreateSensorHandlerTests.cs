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
    public class CreateSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_Sensor_And_Returns_Response()
        {
            // Arrange
            var sensors = new List<Sensor>().AsQueryable();
            var mockSet = new Mock<DbSet<Sensor>>();
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.Provider).Returns(sensors.Provider);
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.Expression).Returns(sensors.Expression);
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.ElementType).Returns(sensors.ElementType);
            mockSet.As<IQueryable<Sensor>>().Setup(m => m.GetEnumerator()).Returns(sensors.GetEnumerator());
            mockSet.Setup(m => m.Add(It.IsAny<Sensor>())).Callback<Sensor>(s => s.Id = 123);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChanges()).Returns(1);

            var handler = new CreateSensorHandler(mockContext.Object);
            var request = new CreateSensorRequest
            {
                UserId = 1,
                Pivot = new Pivot { Id = 2 },
                Code = "SENSOR-1",
                Quadrante = 3
            };

            // Act
            var result = await handler.Handle(request, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
        }

        [Fact]
        public async Task Handle_Throws_If_UserId_Is_Null()
        {
            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            var handler = new CreateSensorHandler(mockContext.Object);
            var request = new CreateSensorRequest
            {
                UserId = null,
                Pivot = new Pivot { Id = 2 },
                Code = "SENSOR-1",
                Quadrante = 3
            };

            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(request, default));
        }

        [Fact]
        public async Task Handle_Throws_If_Pivot_Is_Null_Or_Id_Invalid()
        {
            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            var handler = new CreateSensorHandler(mockContext.Object);

            // Null Pivot
            var requestNullPivot = new CreateSensorRequest
            {
                UserId = 1,
                Pivot = null,
                Code = "SENSOR-1",
                Quadrante = 3
            };
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(requestNullPivot, default));

            // Invalid Pivot Id
            var requestInvalidPivot = new CreateSensorRequest
            {
                UserId = 1,
                Pivot = new Pivot { Id = 0 },
                Code = "SENSOR-1",
                Quadrante = 3
            };
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(requestInvalidPivot, default));
        }
    }
}