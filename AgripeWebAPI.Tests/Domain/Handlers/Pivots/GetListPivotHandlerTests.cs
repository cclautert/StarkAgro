using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetListPivotHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Pivots_For_UserId()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(5);
            var projected = new List<GetPivotResponse>
            {
                new GetPivotResponse { Id = 1, Name = "Pivot A" },
                new GetPivotResponse { Id = 2, Name = "Pivot B" }
            };
            MongoMockHelper.SetupFindProjection<Pivot, GetPivotResponse>(mockPivots, projected);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetListPivotHandler(mockDbContext.Object, mockCurrentUser.Object);
            var result = await handler.Handle(new GetListPivotByUserIdRequest(), default);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, p => p.Id == 1 && p.Name == "Pivot A");
            Assert.Contains(result, p => p.Id == 2 && p.Name == "Pivot B");
        }

        [Fact]
        public async Task Handle_Returns_EmptyList_When_No_Pivots_For_UserId()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(100);
            MongoMockHelper.SetupFindProjection<Pivot, GetPivotResponse>(mockPivots, new List<GetPivotResponse>());
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetListPivotHandler(mockDbContext.Object, mockCurrentUser.Object);
            var result = await handler.Handle(new GetListPivotByUserIdRequest(), default);

            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
