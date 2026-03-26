using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class EditPivotHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_Pivot_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var pivot = new Pivot { Id = 1, Name = "OldName", UserId = 1 };
            MongoMockHelper.SetupFind(mockPivots, pivot);
            mockPivots.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<Pivot>>(), It.IsAny<Pivot>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new EditPivotHandler(mockDbContext.Object);
            var result = await handler.Handle(new EditPivotRequest { Id = 1, Name = "NewName" }, default);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("NewName", pivot.Name);
        }

        [Fact]
        public async Task Handle_Throws_When_Pivot_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            MongoMockHelper.SetupFind<Pivot>(mockPivots, null);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new EditPivotHandler(mockDbContext.Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new EditPivotRequest { Id = 999, Name = "X" }, default));
        }
    }
}
