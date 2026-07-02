using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class GetUserAlertsHandlerTests
    {
        private static (Mock<agpDBContext> db, Mock<ICurrentUserContext> currentUser) BuildMocks(
            List<IrrigationAlert>? irrigationAlerts = null,
            List<SensorAnomaly>? anomalies = null,
            User? user = null,
            List<Pivot>? pivots = null,
            List<Sensor>? sensors = null,
            int? userId = 1)
        {
            var db = new Mock<agpDBContext>();

            var irrigationCol = new Mock<IMongoCollection<IrrigationAlert>>();
            MongoMockHelper.SetupFindList(irrigationCol, irrigationAlerts ?? new List<IrrigationAlert>());
            db.Setup(d => d.IrrigationAlerts).Returns(irrigationCol.Object);

            var anomaliesCol = new Mock<IMongoCollection<SensorAnomaly>>();
            MongoMockHelper.SetupFindList(anomaliesCol, anomalies ?? new List<SensorAnomaly>());
            db.Setup(d => d.SensorAnomalies).Returns(anomaliesCol.Object);

            var usersCol = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFind(usersCol, user);
            db.Setup(d => d.Users).Returns(usersCol.Object);

            var pivotsCol = new Mock<IMongoCollection<Pivot>>();
            MongoMockHelper.SetupFindList(pivotsCol, pivots ?? new List<Pivot>());
            db.Setup(d => d.Pivots).Returns(pivotsCol.Object);

            var sensorsCol = new Mock<IMongoCollection<Sensor>>();
            MongoMockHelper.SetupFindList(sensorsCol, sensors ?? new List<Sensor>());
            db.Setup(d => d.Sensors).Returns(sensorsCol.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(userId);

            return (db, currentUser);
        }

        [Fact]
        public async Task Handle_MergesIrrigationAndAnomalyAlerts_OrderedByDateDesc()
        {
            var now = DateTime.UtcNow;
            var irrigation = new List<IrrigationAlert>
            {
                new() { Id = 10, UserId = 1, PivotId = 5, ProjectedValue = 22, LimiteInferior = 25, Date = now.AddHours(-2) }
            };
            var anomalies = new List<SensorAnomaly>
            {
                new() { Id = 20, UserId = 1, PivotId = 5, SensorId = 7, Value = 95, ExpectedMin = 30, ExpectedMax = 80, Date = now.AddHours(-1) }
            };
            var pivots = new List<Pivot> { new() { Id = 5, Name = "Pivo Central" } };
            var sensors = new List<Sensor> { new() { Id = 7, Code = "S-07", Quadrante = 1 } };
            var (db, currentUser) = BuildMocks(irrigation, anomalies, new User { Id = 1 }, pivots, sensors);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
            // Most recent first (anomaly)
            Assert.Equal("anomaly-20", result[0].Id);
            Assert.Equal("AnomalyPersisted", result[0].AlertType);
            Assert.Contains("S-07", result[0].Title);
            Assert.Equal("Pivo Central", result[0].PivotName);
            Assert.Equal("irrigation-10", result[1].Id);
            Assert.Equal("MoistureLow", result[1].AlertType);
            Assert.Contains("22", result[1].Title);
            Assert.Contains("25", result[1].Title);
            Assert.All(result, a => Assert.False(a.IsRead));
        }

        [Fact]
        public async Task Handle_AlertsReadAtSet_MarksOlderAlertsAsRead()
        {
            var now = DateTime.UtcNow;
            var irrigation = new List<IrrigationAlert>
            {
                new() { Id = 1, UserId = 1, PivotId = 5, Date = now.AddHours(-3) },
                new() { Id = 2, UserId = 1, PivotId = 5, Date = now.AddMinutes(-10) }
            };
            var user = new User { Id = 1, AlertsReadAt = now.AddHours(-1) };
            var (db, currentUser) = BuildMocks(irrigation, user: user);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal("irrigation-2", result[0].Id);
            Assert.False(result[0].IsRead);
            Assert.Equal("irrigation-1", result[1].Id);
            Assert.True(result[1].IsRead);
        }

        [Fact]
        public async Task Handle_UnknownPivotAndSensor_UsesFallbackLabels()
        {
            var anomalies = new List<SensorAnomaly>
            {
                new() { Id = 3, UserId = 1, PivotId = 99, SensorId = 88, Value = 5, ExpectedMin = 30, ExpectedMax = 80, Date = DateTime.UtcNow }
            };
            var (db, currentUser) = BuildMocks(anomalies: anomalies, user: new User { Id = 1 });
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Pivô 99", result[0].PivotName);
            Assert.StartsWith("Leitura", result[0].Title);
        }

        [Fact]
        public async Task Handle_NoAuthenticatedUser_ThrowsInvalidOperationException()
        {
            var (db, currentUser) = BuildMocks(userId: null);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(new GetUserAlertsRequest(), CancellationToken.None));
        }

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            var (db, currentUser) = BuildMocks();
            Assert.Throws<ArgumentNullException>(() => new GetUserAlertsHandler(null!, currentUser.Object));
            Assert.Throws<ArgumentNullException>(() => new GetUserAlertsHandler(db.Object, null!));
        }
    }
}
