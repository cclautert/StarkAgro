using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class DeletePivotHandlerTests
    {
        [Fact]
        public async Task Handle_Deletes_Pivot_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupDeleteOne(mockPivots, 1);
            MongoMockHelper.SetupDeleteMany(mockSensors);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new DeletePivotHandler(mockDbContext.Object);
            var result = await handler.Handle(new DeletePivotRequest { Id = 3 }, default);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task Handle_Throws_When_Pivot_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            MongoMockHelper.SetupDeleteOne(mockPivots, 0);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new DeletePivotHandler(mockDbContext.Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new DeletePivotRequest { Id = 999 }, default));
        }
    }
}
