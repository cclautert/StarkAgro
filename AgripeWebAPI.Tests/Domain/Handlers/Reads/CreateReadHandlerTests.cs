using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Domain.Handlers.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class CreateReadHandlerTests
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
        public async Task Handle_Should_Add_ReadSensor_And_Save_When_Sensor_Exists()
        {
            // Arrange
            var sensor = new Sensor { Id = 1, Code = "SENSOR-1" }; // Ensure the correct 'Sensor' type is used
            var sensors = new List<Sensor> { sensor }.AsQueryable(); // Correctly use the 'Sensor' type from the imported namespace
            var mockSensors = CreateMockDbSet(sensors);

            var readSensors = new List<ReadSensor>().AsQueryable();
            var mockReadSensors = CreateMockDbSet(readSensors);

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);
            mockContext.Setup(c => c.SaveChanges()).Returns(1);

            mockReadSensors.Setup(m => m.Add(It.IsAny<ReadSensor>()))
                .Callback<ReadSensor>(rs => { /* Optionally verify properties here */ });

            var handler = new CreateReadHandler(mockContext.Object);
            var request = new CreateReadRequest { Code = "SENSOR-1", Value = 42.5m };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            mockReadSensors.Verify(m => m.Add(It.Is<ReadSensor>(rs =>
                rs.SensorId == 1 &&
                rs.Value == 42.5m
            )), Times.Once);

            mockContext.Verify(c => c.SaveChanges(), Times.Once);
            Assert.NotNull(result);
            Assert.IsType<CreateReadResponse>(result);
        }

        [Fact(Skip = "Temporarily disabled features")]
        public async Task Handle_Should_Throw_When_Sensor_Does_Not_Exist()
        {
            // Arrange
            var sensors = new List<Sensor>().AsQueryable(); // Ensure the correct 'Sensor' type is used
            var mockSensors = CreateMockDbSet(sensors);

            var mockReadSensors = CreateMockDbSet(new List<ReadSensor>().AsQueryable());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockContext.Setup(c => c.ReadSensors).Returns(mockReadSensors.Object);

            var handler = new CreateReadHandler(mockContext.Object);
            var request = new CreateReadRequest { Code = "NOT-FOUND", Value = 10m };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(request, CancellationToken.None));
        }
    }
}