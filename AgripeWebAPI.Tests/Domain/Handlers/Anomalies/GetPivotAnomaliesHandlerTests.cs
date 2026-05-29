using AgripeWebAPI.Domain.Commands.Requests.Anomalies;
using AgripeWebAPI.Domain.Handlers.Anomalies;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using AgripeWebAPI.Tests.Mocks;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Anomalies
{
    public class GetPivotAnomaliesHandlerTests
    {
        private readonly Mock<agpDBContext> _mockDbContext;
        private readonly Mock<IMongoCollection<Pivot>> _mockPivots;
        private readonly Mock<IMongoCollection<Sensor>> _mockSensors;
        private readonly Mock<IMongoCollection<SensorAnomaly>> _mockSensorAnomalies;
        private readonly Mock<ICurrentUserContext> _mockCurrentUser;
        private readonly MockNotifier _notifier;
        private readonly GetPivotAnomaliesHandler _handler;

        public GetPivotAnomaliesHandlerTests()
        {
            _mockDbContext = new Mock<agpDBContext>();
            _mockPivots = new Mock<IMongoCollection<Pivot>>();
            _mockSensors = new Mock<IMongoCollection<Sensor>>();
            _mockSensorAnomalies = new Mock<IMongoCollection<SensorAnomaly>>();
            _mockCurrentUser = new Mock<ICurrentUserContext>();
            _notifier = new MockNotifier();

            _mockDbContext.Setup(db => db.Pivots).Returns(_mockPivots.Object);
            _mockDbContext.Setup(db => db.Sensors).Returns(_mockSensors.Object);
            _mockDbContext.Setup(db => db.SensorAnomalies).Returns(_mockSensorAnomalies.Object);
            _mockCurrentUser.Setup(u => u.UserId).Returns(42);

            _handler = new GetPivotAnomaliesHandler(_mockDbContext.Object, _mockCurrentUser.Object, _notifier);
        }

        [Fact]
        public async Task Handle_InvalidPivotId_ShouldNotifyAndReturnEmpty()
        {
            var request = new GetPivotAnomaliesRequest { PivotId = 0, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Empty(result);
            Assert.True(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_PivotNotFound_ShouldNotifyAndReturnEmpty()
        {
            MongoMockHelper.SetupFind<Pivot>(_mockPivots, null);

            var request = new GetPivotAnomaliesRequest { PivotId = 1, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Empty(result);
            Assert.True(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_NoSensors_ShouldReturnEmpty()
        {
            MongoMockHelper.SetupFind(_mockPivots, new Pivot { Id = 1, UserId = 42, Name = "P1" });
            MongoMockHelper.SetupFindList(_mockSensors, new List<Sensor>());

            var request = new GetPivotAnomaliesRequest { PivotId = 1, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Empty(result);
            _mockSensorAnomalies.Verify(c => c.FindAsync(
                It.IsAny<FilterDefinition<SensorAnomaly>>(),
                It.IsAny<FindOptions<SensorAnomaly, SensorAnomaly>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WithAnomalies_ShouldReturnMappedResponses()
        {
            var pivot = new Pivot { Id = 1, UserId = 42, Name = "P1" };
            var sensors = new List<Sensor> { new Sensor { Id = 10, PivoId = 1, UserId = 42 } };
            var anomalies = new List<SensorAnomaly>
            {
                new SensorAnomaly
                {
                    Id = 5, SensorId = 10, UserId = 42, ReadSensorId = 100,
                    Value = 999m, ExpectedMin = 40m, ExpectedMax = 60m,
                    Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Acknowledged = false
                }
            };

            MongoMockHelper.SetupFind(_mockPivots, pivot);
            MongoMockHelper.SetupFindList(_mockSensors, sensors);
            MongoMockHelper.SetupFindList(_mockSensorAnomalies, anomalies);

            var request = new GetPivotAnomaliesRequest { PivotId = 1, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(5, result[0].Id);
            Assert.Equal(10, result[0].SensorId);
            Assert.Equal(100, result[0].ReadSensorId);
            Assert.Equal(999m, result[0].Value);
            Assert.Equal(40m, result[0].ExpectedMin);
            Assert.Equal(60m, result[0].ExpectedMax);
            Assert.False(result[0].Acknowledged);
        }

        [Fact]
        public async Task Handle_TenantIsolation_ShouldFilterByUserId()
        {
            var pivot = new Pivot { Id = 1, UserId = 42, Name = "P1" };
            var sensors = new List<Sensor> { new Sensor { Id = 10, PivoId = 1, UserId = 42 } };

            MongoMockHelper.SetupFind(_mockPivots, pivot);
            MongoMockHelper.SetupFindList(_mockSensors, sensors);
            MongoMockHelper.SetupFindList(_mockSensorAnomalies, new List<SensorAnomaly>());

            var request = new GetPivotAnomaliesRequest { PivotId = 1, UserId = 42 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Empty(result);
            Assert.False(_notifier.HasNotification());
        }

        [Fact]
        public async Task Handle_PivotBelongsToDifferentUser_ShouldReturnEmpty()
        {
            MongoMockHelper.SetupFind<Pivot>(_mockPivots, null);

            var request = new GetPivotAnomaliesRequest { PivotId = 1, UserId = 99 };

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.Empty(result);
            Assert.True(_notifier.HasNotification());
        }
    }
}
