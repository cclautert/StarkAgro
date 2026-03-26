using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetPivotHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Pivot_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var expected = new GetPivotResponse { Id = 2, Name = "Pivot2" };
            MongoMockHelper.SetupFindProjection<Pivot, GetPivotResponse>(mockPivots, new List<GetPivotResponse> { expected });
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetPivotHandler(mockDbContext.Object);
            var result = await handler.Handle(new GetPivotRequest { Id = 2 }, default);

            Assert.NotNull(result);
            Assert.Equal(2, result!.Id);
            Assert.Equal("Pivot2", result.Name);
        }
    }
}
