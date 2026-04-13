using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class EditSensorHandlerTests_MacNormalisation
    {
        private static (Mock<agpDBContext> db, Mock<IMongoCollection<Sensor>> sensors)
            BuildMocks(Sensor existingSensor)
        {
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupFind(mockSensors, existingSensor);
            mockSensors
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Sensor>>(),
                    It.IsAny<Sensor>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);

            return (mockDb, mockSensors);
        }

        [Fact]
        public async Task Handle_NormalisesCodeToUppercase_OnEdit()
        {
            // Arrange
            var existing = new Sensor { Id = 5, Code = "OLD:CODE:HERE:XX:XX:XX", Quadrante = 1, PivoId = 1 };
            var (mockDb, _) = BuildMocks(existing);

            var handler = new EditSensorHandler(mockDb.Object);
            var request = new EditSensorRequest
            {
                Id = 5,
                Code = "aa:bb:cc:dd:ee:ff",
                Pivot = new Pivot { Id = 1 },
                Quadrante = 2
            };

            // Act
            await handler.Handle(request, default);

            // Assert — sensor is mutated in place before ReplaceOneAsync
            Assert.Equal("AA:BB:CC:DD:EE:FF", existing.Code);
        }

        [Fact]
        public async Task Handle_NullCode_IsStoredAsNull()
        {
            // Arrange
            var existing = new Sensor { Id = 6, Code = "5C:CF:7F:3A:54:29", Quadrante = 1, PivoId = 1 };
            var (mockDb, _) = BuildMocks(existing);

            var handler = new EditSensorHandler(mockDb.Object);
            var request = new EditSensorRequest
            {
                Id = 6,
                Code = null,
                Pivot = new Pivot { Id = 1 },
                Quadrante = 1
            };

            // Act
            await handler.Handle(request, default);

            // Assert
            Assert.Null(existing.Code);
        }
    }
}
