using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Handlers.Sensor;
using AgripeWebAPI.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class GetSensorHandlerTests
    {
        private static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> data) where T : class
        {
            var mockSet = new Mock<DbSet<T>>();
            mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(data.Provider);
            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
            return mockSet;
        }

        [Fact(Skip = "Temporarily disabled features")]
        public async Task Handle_Returns_Sensor_When_Code_Exists()
        {
            // Arrange
            var code = "SENSOR-123";
            var sensors = new List<Models.Entities.Sensor>
            {
                new Models.Entities.Sensor { Id = 1, PivoId = 2, UserId = 3, Quadrante = 4, Code = code }
            }.AsQueryable();

            var mockSensors = CreateMockDbSet(sensors);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetSensorHandler(mockContext.Object);
            var request = new GetSensorRequest { Code = code };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal(2, result.PivoId);
            Assert.Equal(3, result.UserId);
            Assert.Equal(4, result.Quadrante);
            Assert.Equal(code, result.Code);
        }

        [Fact(Skip = "Temporarily disabled features")]
        public async Task Handle_Returns_Null_When_Code_Does_Not_Exist()
        {
            // Arrange
            var sensors = new List<Models.Entities.Sensor>
            {
                new Models.Entities.Sensor { Id = 1, PivoId = 2, UserId = 3, Quadrante = 4, Code = "OTHER" }
            }.AsQueryable();

            var mockSensors = CreateMockDbSet(sensors);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetSensorHandler(mockContext.Object);
            var request = new GetSensorRequest { Code = "NOT-FOUND" };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Constructor_Throws_If_DbContext_Is_Null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GetSensorHandler(null));
        }
    }
}