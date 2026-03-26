using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class CreateSensorHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_Sensor_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(1);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("Sensor", It.IsAny<CancellationToken>())).ReturnsAsync(123);
            mockSensors.Setup(c => c.InsertOneAsync(It.IsAny<Sensor>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreateSensorHandler(mockDbContext.Object, mockCurrentUser.Object);
            var request = new CreateSensorRequest
            {
                Pivot = new Pivot { Id = 2 },
                Code = "SENSOR-1",
                Quadrante = 3
            };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
        }

        [Fact]
        public async Task Handle_Throws_If_User_Not_Authenticated()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();
            mockCurrentUser.Setup(u => u.UserId).Returns((int?)null);

            var handler = new CreateSensorHandler(mockDbContext.Object, mockCurrentUser.Object);
            var request = new CreateSensorRequest
            {
                Pivot = new Pivot { Id = 2 },
                Code = "SENSOR-1",
                Quadrante = 3
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(request, default));
        }

        [Fact]
        public async Task Handle_Throws_If_Pivot_Is_Null_Or_Id_Invalid()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();
            mockCurrentUser.Setup(u => u.UserId).Returns(1);

            var handler = new CreateSensorHandler(mockDbContext.Object, mockCurrentUser.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Handle(new CreateSensorRequest { Pivot = null, Code = "S", Quadrante = 1 }, default));

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.Handle(new CreateSensorRequest { Pivot = new Pivot { Id = 0 }, Code = "S", Quadrante = 1 }, default));
        }
    }
}
