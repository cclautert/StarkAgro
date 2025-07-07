using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class GetListSensorHandlerTests
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
        public async Task Handle_Returns_Sensors_For_PivotId()
        {
            // Arrange
            var pivotId = 5;
            var userId = 5;
            var sensors = new List<Models.Entities.Sensor>
            {
                new Models.Entities.Sensor { Id = 1, PivoId = pivotId, UserId = 10, Code = "A", Quadrante = 1 },
                new Models.Entities.Sensor { Id = 2, PivoId = pivotId, UserId = 11, Code = "B", Quadrante = 2 }
            }.AsQueryable();

            var mockSensors = CreateMockDbSet(sensors);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetListSensorHandler(mockContext.Object);
            var request = new GetListSensorByUserIdRequest { UserId = userId };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, s => s.Id == 1 && s.Code == "A" && s.Quadrante == 1);
            Assert.Contains(result, s => s.Id == 2 && s.Code == "B" && s.Quadrante == 2);
        }

        [Fact(Skip = "Temporarily disabled features")]
        public async Task Handle_Returns_EmptyList_When_No_Sensors_For_PivotId()
        {
            // Arrange
            var pivotId = 99;
            var userId = 99;
            var sensors = new List<Models.Entities.Sensor>
            {
                new Models.Entities.Sensor { Id = 1, PivoId = 1, UserId = 10, Code = "A", Quadrante = 1 }
            }.AsQueryable();

            var mockSensors = CreateMockDbSet(sensors);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetListSensorHandler(mockContext.Object);
            var request = new GetListSensorByUserIdRequest { UserId = userId };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Constructor_Throws_If_DbContext_Is_Null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GetListSensorHandler(null));
        }
    }
}