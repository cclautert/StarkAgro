using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using StarkAgroAPI.Domain.Handlers.Sensors;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Sensors
{
    public class GetSensorHandlerTests
    {
        private const int OwnerUserId = 3;

        private static Mock<ICurrentUserContext> BuildCurrentUser(int? userId = OwnerUserId)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(u => u.UserId).Returns(userId);
            return mock;
        }

        [Fact]
        public async Task Handle_Returns_Sensor_When_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var sensor = new Sensor { Id = 123, PivoId = 2, UserId = OwnerUserId, Quadrante = 4, Code = "SENSOR-123" };
            MongoMockHelper.SetupFind(mockSensors, sensor);
            MongoMockHelper.SetupFind(mockPivots, new Pivot { Id = 2, Name = "Pivot2" });
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetSensorHandler(mockDbContext.Object, BuildCurrentUser().Object);
            var result = await handler.Handle(new GetSensorRequest { Id = 123 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(123, result!.Id);
            Assert.Equal(2, result.Pivot.Id);
            Assert.Equal(4, result.Quadrante);
            Assert.Equal("SENSOR-123", result.Code);
        }

        [Fact]
        public async Task Handle_Returns_Null_When_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupFind<Sensor>(mockSensors, null);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetSensorHandler(mockDbContext.Object, BuildCurrentUser().Object);
            var result = await handler.Handle(new GetSensorRequest { Id = 0 }, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_DifferentTenant_ReturnsNull()
        {
            // Tenant scoping: Find filter includes UserId so cross-tenant lookup yields null.
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupFind<Sensor>(mockSensors, null);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetSensorHandler(mockDbContext.Object, BuildCurrentUser(999).Object);
            var result = await handler.Handle(new GetSensorRequest { Id = 123 }, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_UnauthenticatedUser_Throws()
        {
            var mockDbContext = new Mock<agpDBContext>();

            var handler = new GetSensorHandler(mockDbContext.Object, BuildCurrentUser(null).Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new GetSensorRequest { Id = 1 }, CancellationToken.None));
        }

        [Fact]
        public void Constructor_Throws_If_DbContext_Is_Null()
        {
            var currentUser = BuildCurrentUser();
            Assert.Throws<ArgumentNullException>(() => new GetSensorHandler(null!, currentUser.Object));
        }

        [Fact]
        public void Constructor_Throws_If_CurrentUser_Is_Null()
        {
            var mockDbContext = new Mock<agpDBContext>();
            Assert.Throws<ArgumentNullException>(() => new GetSensorHandler(mockDbContext.Object, null!));
        }
    }
}
