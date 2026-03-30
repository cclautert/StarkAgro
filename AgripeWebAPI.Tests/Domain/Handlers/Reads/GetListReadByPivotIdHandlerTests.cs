using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class GetListReadBySensorIdHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<ReadSensor>> _mockReadSensors;

        public GetListReadBySensorIdHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockReadSensors = new Mock<IMongoCollection<ReadSensor>>();

            _mockDbContext.Setup(c => c.ReadSensors).Returns(_mockReadSensors.Object);
        }

        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GetListReadBySensorIdHandler(null!));
        }

        [Fact]
        public async Task Handle_NoReadsForSensor_ReturnsEmpty()
        {
            // Arrange
            MongoMockHelper.SetupFindProjection<ReadSensor, GetAllReadBySensorIdResponse>(
                _mockReadSensors, new List<GetAllReadBySensorIdResponse>());

            var handler = new GetListReadBySensorIdHandler(_mockDbContext.Object);
            var request = new GetAllListReadBySensorIdRequest { SensorId = 99, Quadrante = 1, NumberOfReads = 10 };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var items = new List<GetAllReadBySensorIdResponse>();
            await foreach (var item in result)
            {
                items.Add(item);
            }
            Assert.Empty(items);
        }

        [Fact]
        public async Task Handle_SensorExists_ReturnsReads()
        {
            // Arrange
            var expectedReads = new List<GetAllReadBySensorIdResponse>
            {
                new() { Id = 100, SensorId = 1, Value = 25.5m, Date = DateTime.UtcNow.AddHours(-1) },
                new() { Id = 101, SensorId = 1, Value = 30.0m, Date = DateTime.UtcNow }
            };
            MongoMockHelper.SetupFindProjection<ReadSensor, GetAllReadBySensorIdResponse>(_mockReadSensors, expectedReads);

            var handler = new GetListReadBySensorIdHandler(_mockDbContext.Object);
            var request = new GetAllListReadBySensorIdRequest { SensorId = 1, Quadrante = 2, NumberOfReads = 10 };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var items = new List<GetAllReadBySensorIdResponse>();
            await foreach (var item in result)
            {
                items.Add(item);
            }
            Assert.Equal(2, items.Count);
            Assert.Equal(100, items[0].Id);
            Assert.Equal(25.5m, items[0].Value);
            Assert.Equal(101, items[1].Id);
            Assert.Equal(30.0m, items[1].Value);
        }
    }
}
