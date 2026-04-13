using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class CreateSensorHandlerTests_MacNormalisation
    {
        private static (Mock<agpDBContext> db, Mock<IMongoCollection<Sensor>> sensors, Mock<ICurrentUserContext> user)
            BuildMocks(int nextId = 1)
        {
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockUser = new Mock<ICurrentUserContext>();

            mockUser.Setup(u => u.UserId).Returns(1);
            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.GetNextIdAsync("Sensor", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(nextId);
            mockSensors
                .Setup(c => c.InsertOneAsync(It.IsAny<Sensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return (mockDb, mockSensors, mockUser);
        }

        [Fact]
        public async Task Handle_NormalisesCodeToUppercase()
        {
            // Arrange
            var (mockDb, mockSensors, mockUser) = BuildMocks();
            Sensor? capturedSensor = null;
            mockSensors
                .Setup(c => c.InsertOneAsync(It.IsAny<Sensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<Sensor, InsertOneOptions, CancellationToken>((s, _, _) => capturedSensor = s)
                .Returns(Task.CompletedTask);

            var handler = new CreateSensorHandler(mockDb.Object, mockUser.Object);
            var request = new CreateSensorRequest
            {
                Code = "5c:cf:7f:3a:54:29",
                Pivot = new Pivot { Id = 1 },
                Quadrante = 1
            };

            // Act
            await handler.Handle(request, default);

            // Assert
            Assert.NotNull(capturedSensor);
            Assert.Equal("5C:CF:7F:3A:54:29", capturedSensor!.Code);
        }

        [Fact]
        public async Task Handle_AlreadyUppercaseCode_StoredUnchanged()
        {
            // Arrange
            var (mockDb, mockSensors, mockUser) = BuildMocks();
            Sensor? capturedSensor = null;
            mockSensors
                .Setup(c => c.InsertOneAsync(It.IsAny<Sensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<Sensor, InsertOneOptions, CancellationToken>((s, _, _) => capturedSensor = s)
                .Returns(Task.CompletedTask);

            var handler = new CreateSensorHandler(mockDb.Object, mockUser.Object);
            var request = new CreateSensorRequest
            {
                Code = "5C:CF:7F:3A:54:29",
                Pivot = new Pivot { Id = 1 },
                Quadrante = 1
            };

            // Act
            await handler.Handle(request, default);

            // Assert
            Assert.NotNull(capturedSensor);
            Assert.Equal("5C:CF:7F:3A:54:29", capturedSensor!.Code);
        }
    }
}
