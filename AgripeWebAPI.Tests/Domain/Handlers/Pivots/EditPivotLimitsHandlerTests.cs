using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class EditPivotLimitsHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_PivotLimits_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var pivot = new Pivot { Id = 1, Name = "Pivot1", UserId = 1 };
            MongoMockHelper.SetupFind(mockPivots, pivot);
            mockPivots.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Pivot>>(), It.IsAny<Pivot>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new EditPivotLimitsHandler(mockDbContext.Object);
            var result = await handler.Handle(
                new EditPivotLimitsRequest { Id = 1, LimiteInferior = 10.5m, LimiteSuperior = 99.9m }, default);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal(10.5m, pivot.LimiteInferior);
            Assert.Equal(99.9m, pivot.LimiteSuperior);
        }

        [Fact]
        public async Task Handle_Throws_When_Pivot_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            MongoMockHelper.SetupFind<Pivot>(mockPivots, null);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new EditPivotLimitsHandler(mockDbContext.Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new EditPivotLimitsRequest { Id = 999 }, default));
        }

        [Fact]
        public async Task Handle_PersistsRainThresholdMm_WhenProvided()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var pivot = new Pivot { Id = 1, Name = "Pivot1", UserId = 1 };
            MongoMockHelper.SetupFind(mockPivots, pivot);
            mockPivots.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Pivot>>(), It.IsAny<Pivot>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new EditPivotLimitsHandler(mockDbContext.Object);
            await handler.Handle(
                new EditPivotLimitsRequest { Id = 1, LimiteInferior = null, LimiteSuperior = null, RainThresholdMm = 3.5 }, default);

            Assert.Equal(3.5, pivot.RainThresholdMm);
        }

        [Fact]
        public async Task Handle_ClearsRainThresholdMm_WhenNull()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var pivot = new Pivot { Id = 1, Name = "Pivot1", UserId = 1, RainThresholdMm = 3.5 };
            MongoMockHelper.SetupFind(mockPivots, pivot);
            mockPivots.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Pivot>>(), It.IsAny<Pivot>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new EditPivotLimitsHandler(mockDbContext.Object);
            await handler.Handle(
                new EditPivotLimitsRequest { Id = 1, LimiteInferior = null, LimiteSuperior = null, RainThresholdMm = null }, default);

            Assert.Null(pivot.RainThresholdMm);
        }
    }
}
