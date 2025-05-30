using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using AgripeWebAPI.Domain.Handlers.Sensor;
using AgripeWebAPI.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class CreateSensorHandlerTests
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
        public async Task Handle_Should_Add_Sensor_And_Return_Response()
        {
            // Arrange
            var sensors = new List<Models.Entities.Sensor>().AsQueryable();
            var mockSensors = CreateMockDbSet(sensors);

            // Simulate EF Core's Add by setting the Id after add
            mockSensors.Setup(m => m.Add(It.IsAny<Models.Entities.Sensor>()))
                .Callback<Models.Entities.Sensor>(s => s.Id = 123);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockContext.Setup(c => c.SaveChanges()).Returns(1);

            var handler = new CreateSensorHandler(mockContext.Object);
            var request = new CreateSensorRequest
            {
                PivoId = 1,
                UserId = 2,
                Code = "CODE-001",
                Quadrante = 3
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            mockSensors.Verify(m => m.Add(It.Is<Models.Entities.Sensor>(s =>
                s.PivoId == 1 &&
                s.UserId == 2 &&
                s.Code == "CODE-001" &&
                s.Quadrante == 3
            )), Times.Once);

            mockContext.Verify(c => c.SaveChanges(), Times.Once);
            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
        }

        [Fact]
        public void Constructor_Should_Throw_If_DbContext_Is_Null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CreateSensorHandler(null));
        }
    }
}