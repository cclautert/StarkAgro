using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using StarkAgroAPI.Domain.Handlers.Sensors;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Sensors
{
    public class GetListSensorByUserIdHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<Pivot>> _mockPivots;

        public GetListSensorByUserIdHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockPivots = new Mock<IMongoCollection<Pivot>>();

            _mockDbContext.Setup(c => c.Sensors).Returns(_mockSensors.Object);
            _mockDbContext.Setup(c => c.Pivots).Returns(_mockPivots.Object);
        }

        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GetListSensorByUserIdHandler(null!));
        }

        [Fact]
        public async Task Handle_ReturnsSensorsWithPivot()
        {
            // Arrange
            var sensors = new List<Sensor>
            {
                new() { Id = 1, PivoId = 10, Quadrante = 1, Name = "Sensor A", Code = "SA" },
                new() { Id = 2, PivoId = 10, Quadrante = 1, Name = "Sensor B", Code = "SB" }
            };
            var pivot = new Pivot { Id = 10, UserId = 5, Name = "Pivot One" };

            MongoMockHelper.SetupFindList(_mockSensors, sensors);
            MongoMockHelper.SetupFind(_mockPivots, pivot);

            var handler = new GetListSensorByUserIdHandler(_mockDbContext.Object);
            var request = new GetListSensorRequest { PivotId = 10, Quadrante = 1 };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Sensor A", result[0].Name);
            Assert.Equal("SA", result[0].Code);
            Assert.Equal(10, result[0].Pivot.Id);
            Assert.Equal("Pivot One", result[0].Pivot.Name);
            Assert.Equal(1, result[0].Quadrante);
            Assert.Equal("Sensor B", result[1].Name);
        }

        [Fact]
        public async Task Handle_NoPivotFound_UsesFallback()
        {
            // Arrange
            var sensors = new List<Sensor>
            {
                new() { Id = 1, PivoId = 10, Quadrante = 2, Name = "Sensor C", Code = "SC" }
            };

            MongoMockHelper.SetupFindList(_mockSensors, sensors);
            MongoMockHelper.SetupFind<Pivot>(_mockPivots, null); // No pivot found

            var handler = new GetListSensorByUserIdHandler(_mockDbContext.Object);
            var request = new GetListSensorRequest { PivotId = 10, Quadrante = 2 };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.Equal(10, result[0].Pivot.Id);
            Assert.Null(result[0].Pivot.Name);
        }

        [Fact]
        public async Task Handle_NoSensors_ReturnsEmpty()
        {
            // Arrange
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor>());
            MongoMockHelper.SetupFind<Pivot>(_mockPivots, null);

            var handler = new GetListSensorByUserIdHandler(_mockDbContext.Object);
            var request = new GetListSensorRequest { PivotId = 10, Quadrante = 1 };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Empty(result);
        }
    }
}
