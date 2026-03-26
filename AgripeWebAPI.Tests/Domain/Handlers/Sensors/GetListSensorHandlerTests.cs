using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class GetListSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Sensors_For_User()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(5);
            var sensors = new List<Sensor>
            {
                new Sensor { Id = 1, PivoId = 10, UserId = 5, Code = "A", Quadrante = 1 },
                new Sensor { Id = 2, PivoId = 10, UserId = 5, Code = "B", Quadrante = 2 }
            };
            MongoMockHelper.SetupFindList(mockSensors, sensors);
            MongoMockHelper.SetupFindList(mockPivots, new List<Pivot> { new Pivot { Id = 10, Name = "P1" } });
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetListSensorHandler(mockDbContext.Object, mockCurrentUser.Object);
            var result = await handler.Handle(new GetListSensorByUserIdRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, s => s.Id == 1 && s.Code == "A");
            Assert.Contains(result, s => s.Id == 2 && s.Code == "B");
        }

        [Fact]
        public async Task Handle_Returns_EmptyList_When_No_Sensors()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(99);
            MongoMockHelper.SetupFindList(mockSensors, new List<Sensor>());
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new GetListSensorHandler(mockDbContext.Object, mockCurrentUser.Object);
            var result = await handler.Handle(new GetListSensorByUserIdRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Constructor_Throws_If_DbContext_Is_Null()
        {
            var mockCurrentUser = new Mock<ICurrentUserContext>();
            Assert.Throws<ArgumentNullException>(() => new GetListSensorHandler(null!, mockCurrentUser.Object));
        }
    }
}
