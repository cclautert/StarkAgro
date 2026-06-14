using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Reads
{
    public class GetReadByPivotIdHandlerTests
    {
        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GetReadByPivotIdHandler(null!));
        }

        private static GetListReadByPivotIdRequest CreateRequest(int pivotId = 1, int userId = 1, int numberOfReads = 10)
            => new GetListReadByPivotIdRequest { PivotId = pivotId, UserId = userId, NumberOfReads = numberOfReads };

        [Fact]
        public async Task Handle_NoSensors_ReturnsResponseWithGrayQuadrants()
        {
            // Arrange
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var mockReads = new Mock<IMongoCollection<ReadSensor>>();

            MongoMockHelper.SetupFindProjection<Sensor, SensorSummary>(mockSensors, new List<SensorSummary>());
            MongoMockHelper.SetupFind(mockPivots, new Pivot { Id = 1, Name = "P1" });
            MongoMockHelper.SetupFind(mockUsers, new User { Id = 1, LimiteInferior = 25m, LimiteSuperior = 75m });

            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDb.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDb.Setup(c => c.ReadSensors).Returns(mockReads.Object);

            var handler = new GetReadByPivotIdHandler(mockDb.Object);

            // Act
            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("P1", result.Name);
            Assert.NotNull(result.Quadrante);
            // All quadrants should be gray (no data)
            Assert.Equal("#607D8B", result.Quadrante.TopRight);
            Assert.Equal("#607D8B", result.Quadrante.BottomRight);
            Assert.Equal("#607D8B", result.Quadrante.BottomLeft);
            Assert.Equal("#607D8B", result.Quadrante.TopLeft);
        }

        [Fact]
        public async Task Handle_NoPivotFound_UsesUserLimits()
        {
            // Arrange
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var mockReads = new Mock<IMongoCollection<ReadSensor>>();

            MongoMockHelper.SetupFindProjection<Sensor, SensorSummary>(mockSensors, new List<SensorSummary>());
            MongoMockHelper.SetupFind<Pivot>(mockPivots, null);
            MongoMockHelper.SetupFind(mockUsers, new User { Id = 1, LimiteInferior = 30m, LimiteSuperior = 70m });

            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDb.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDb.Setup(c => c.ReadSensors).Returns(mockReads.Object);

            var handler = new GetReadByPivotIdHandler(mockDb.Object);

            // Act
            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(30m, result.LimiteInferior);
            Assert.Equal(70m, result.LimiteSuperior);
        }

        [Fact]
        public async Task Handle_NoPivotNoUser_UsesDefaultLimits()
        {
            // Arrange
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var mockReads = new Mock<IMongoCollection<ReadSensor>>();

            MongoMockHelper.SetupFindProjection<Sensor, SensorSummary>(mockSensors, new List<SensorSummary>());
            MongoMockHelper.SetupFind<Pivot>(mockPivots, null);
            MongoMockHelper.SetupFind<User>(mockUsers, null);

            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDb.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDb.Setup(c => c.ReadSensors).Returns(mockReads.Object);

            var handler = new GetReadByPivotIdHandler(mockDb.Object);

            // Act
            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(25m, result.LimiteInferior); // default
            Assert.Equal(75m, result.LimiteSuperior); // default
        }

        [Fact]
        public async Task Handle_PivotLimitsTakePrecedenceOverUserLimits()
        {
            // Arrange
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var mockReads = new Mock<IMongoCollection<ReadSensor>>();

            MongoMockHelper.SetupFindProjection<Sensor, SensorSummary>(mockSensors, new List<SensorSummary>());
            MongoMockHelper.SetupFind(mockPivots, new Pivot { Id = 1, Name = "P1", LimiteInferior = 10m, LimiteSuperior = 90m });
            MongoMockHelper.SetupFind(mockUsers, new User { Id = 1, LimiteInferior = 25m, LimiteSuperior = 75m });

            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDb.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDb.Setup(c => c.ReadSensors).Returns(mockReads.Object);

            var handler = new GetReadByPivotIdHandler(mockDb.Object);

            // Act
            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            // Assert
            Assert.Equal(10m, result.LimiteInferior);
            Assert.Equal(90m, result.LimiteSuperior);
        }

        [Fact]
        public async Task Handle_WithSensors_QueriesReadsAndBuildsColoredQuadrants()
        {
            // Arrange
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var mockReads = new Mock<IMongoCollection<ReadSensor>>();

            var sensorSummaries = new List<SensorSummary>
            {
                new SensorSummary(10, 1), // Sensor 10, quadrant 1
                new SensorSummary(11, 2)  // Sensor 11, quadrant 2
            };

            MongoMockHelper.SetupFindProjection<Sensor, SensorSummary>(mockSensors, sensorSummaries);

            var now = DateTime.UtcNow;
            var readsList = new List<ReadSensor>
            {
                new ReadSensor { Id = 1, SensorId = 10, Humidity = 30m, Date = now.AddHours(-1) },
                new ReadSensor { Id = 2, SensorId = 11, Humidity = 80m, Date = now }
            };
            // Call order: 1) ToListAsync for all reads, 2) FirstOrDefault for sensor 10, 3) FirstOrDefault for sensor 11
            mockReads.SetupSequence(c => c.FindAsync(
                    It.IsAny<FilterDefinition<ReadSensor>>(),
                    It.IsAny<FindOptions<ReadSensor, ReadSensor>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(readsList).Object)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<ReadSensor> { readsList[0] }).Object)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<ReadSensor> { readsList[1] }).Object);

            MongoMockHelper.SetupFind(mockPivots, new Pivot { Id = 1, Name = "P1", LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFind(mockUsers, new User { Id = 1, LimiteInferior = 25m, LimiteSuperior = 75m });

            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDb.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDb.Setup(c => c.ReadSensors).Returns(mockReads.Object);

            var handler = new GetReadByPivotIdHandler(mockDb.Object);

            // Act
            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Quadrante);
            // Sensor 10 (quadrant 1), value=30 → between 25 and 75 → green
            Assert.Equal("#4CAF50", result.Quadrante.TopRight);
            // Sensor 11 (quadrant 2), value=80 → above 75 → blue
            Assert.Equal("#2196F3", result.Quadrante.BottomRight);
        }

        [Fact]
        public async Task Handle_ValueBelowLimiteInferior_ShowsRedColor()
        {
            // Arrange
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var mockReads = new Mock<IMongoCollection<ReadSensor>>();

            var sensorSummaries = new List<SensorSummary> { new SensorSummary(5, 3) }; // quadrant 3

            MongoMockHelper.SetupFindProjection<Sensor, SensorSummary>(mockSensors, sensorSummaries);

            var readEntry = new ReadSensor { Id = 1, SensorId = 5, Humidity = 10m, Date = DateTime.UtcNow };
            MongoMockHelper.SetupFindList<ReadSensor>(mockReads, new List<ReadSensor> { readEntry });
            mockReads.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<ReadSensor>>(),
                    It.IsAny<FindOptions<ReadSensor, ReadSensor>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<ReadSensor> { readEntry }).Object);

            MongoMockHelper.SetupFind(mockPivots, new Pivot { Id = 1, Name = "P1", LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFind(mockUsers, new User { Id = 1, LimiteInferior = 25m, LimiteSuperior = 75m });

            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDb.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDb.Setup(c => c.ReadSensors).Returns(mockReads.Object);

            var handler = new GetReadByPivotIdHandler(mockDb.Object);

            // Act
            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            // Assert
            // value=10 < LimiteInferior=25 → red
            Assert.Equal("#F44336", result.Quadrante!.BottomLeft);
        }

        [Fact]
        public async Task Handle_NoDataForQuadrant4_ShowsGrayColor()
        {
            // Arrange — sensors in quadrants 1, 2, 3 only; quadrant 4 is absent
            var mockDb = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockUsers = new Mock<IMongoCollection<User>>();
            var mockReads = new Mock<IMongoCollection<ReadSensor>>();

            var sensorSummaries = new List<SensorSummary>
            {
                new SensorSummary(1, 1), // quadrant 1
                new SensorSummary(2, 2), // quadrant 2
                new SensorSummary(3, 3)  // quadrant 3
            };

            MongoMockHelper.SetupFindProjection<Sensor, SensorSummary>(mockSensors, sensorSummaries);

            var now = DateTime.UtcNow;
            var read1 = new ReadSensor { Id = 1, SensorId = 1, Humidity = 50m, Date = now };
            var read2 = new ReadSensor { Id = 2, SensorId = 2, Humidity = 50m, Date = now };
            var read3 = new ReadSensor { Id = 3, SensorId = 3, Humidity = 50m, Date = now };
            var allReads = new List<ReadSensor> { read1, read2, read3 };

            // First call: ToListAsync for all reads in date range
            // Next three calls: FirstOrDefaultAsync per sensor (latest read for each)
            mockReads.SetupSequence(c => c.FindAsync(
                    It.IsAny<FilterDefinition<ReadSensor>>(),
                    It.IsAny<FindOptions<ReadSensor, ReadSensor>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(allReads).Object)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<ReadSensor> { read1 }).Object)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<ReadSensor> { read2 }).Object)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<ReadSensor> { read3 }).Object);

            MongoMockHelper.SetupFind(mockPivots, new Pivot { Id = 1, Name = "P1", LimiteInferior = 25m, LimiteSuperior = 75m });
            MongoMockHelper.SetupFind(mockUsers, new User { Id = 1, LimiteInferior = 25m, LimiteSuperior = 75m });

            mockDb.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDb.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDb.Setup(c => c.Users).Returns(mockUsers.Object);
            mockDb.Setup(c => c.ReadSensors).Returns(mockReads.Object);

            var handler = new GetReadByPivotIdHandler(mockDb.Object);

            // Act
            var result = await handler.Handle(CreateRequest(), CancellationToken.None);

            // Assert — quadrant 4 (TopLeft) has no sensor, so it falls into the else branch → gray
            Assert.NotNull(result);
            Assert.NotNull(result.Quadrante);
            Assert.Equal("#607D8B", result.Quadrante.TopLeft);
            // Quadrants 1, 2, 3 have data with value=50 (between 25 and 75) → green
            Assert.Equal("#4CAF50", result.Quadrante.TopRight);
            Assert.Equal("#4CAF50", result.Quadrante.BottomRight);
            Assert.Equal("#4CAF50", result.Quadrante.BottomLeft);
        }
    }
}
