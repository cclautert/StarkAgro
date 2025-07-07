using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class GetListReadHandlerTests
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
        public async Task Handle_Returns_Reads_For_User()
        {
            // Arrange
            var userId = 10;
            var sensor = new Sensor { Id = 1, UserId = userId };
            var readSensors = new List<ReadSensor>
            {
                new ReadSensor { Id = 100, SensorId = 1, Value = 12.5m, Sensor = sensor },
                new ReadSensor { Id = 101, SensorId = 1, Value = 15.0m, Sensor = sensor }
            }.AsQueryable();

            var mockReadSensors = CreateMockDbSet(readSensors);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);

            var handler = new GetListReadHandler(mockContext.Object);
            var request = new GetListReadRequest { UserId = userId };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.Id == 100 && r.SensorId == 1 && r.Value == 12.5m);
            Assert.Contains(result, r => r.Id == 101 && r.SensorId == 1 && r.Value == 15.0m);
        }

        [Fact(Skip = "Temporarily disabled features")]
        public async Task Handle_Returns_EmptyList_When_No_Reads_For_User()
        {
            // Arrange
            var userId = 99;
            var sensor = new Sensor { Id = 2, UserId = 1 };
            var readSensors = new List<ReadSensor>
            {
                new ReadSensor { Id = 200, SensorId = 2, Value = 20.0m, Sensor = sensor }
            }.AsQueryable();

            var mockReadSensors = CreateMockDbSet(readSensors);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);

            var handler = new GetListReadHandler(mockContext.Object);
            var request = new GetListReadRequest { UserId = userId };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}