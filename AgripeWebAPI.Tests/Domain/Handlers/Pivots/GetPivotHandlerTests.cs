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
    public class GetPivotHandlerTests
    {
        private const int OwnerUserId = 42;

        private static Mock<ICurrentUserContext> BuildCurrentUser(int? userId = OwnerUserId)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(u => u.UserId).Returns(userId);
            return mock;
        }

        [Fact]
        public async Task Handle_Returns_Pivot_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            var expected = new GetPivotResponse { Id = 2, Name = "Pivot2" };
            MongoMockHelper.SetupFindProjection<Pivot, GetPivotResponse>(mockPivots, new List<GetPivotResponse> { expected });
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetPivotHandler(mockDbContext.Object, BuildCurrentUser().Object);
            var result = await handler.Handle(new GetPivotRequest { Id = 2 }, default);

            Assert.NotNull(result);
            Assert.Equal(2, result!.Id);
            Assert.Equal("Pivot2", result.Name);
        }

        [Fact]
        public async Task Handle_DifferentTenant_ReturnsNull()
        {
            // Tenant scoping: Find filter includes UserId so a different tenant gets no record.
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            MongoMockHelper.SetupFindProjection<Pivot, GetPivotResponse>(mockPivots, new List<GetPivotResponse>());
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new GetPivotHandler(mockDbContext.Object, BuildCurrentUser(999).Object);
            var result = await handler.Handle(new GetPivotRequest { Id = 2 }, default);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_UnauthenticatedUser_Throws()
        {
            var mockDbContext = new Mock<agpDBContext>();

            var handler = new GetPivotHandler(mockDbContext.Object, BuildCurrentUser(null).Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new GetPivotRequest { Id = 1 }, default));
        }
    }
}
