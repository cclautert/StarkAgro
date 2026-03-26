using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class CreatePivotHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_Pivot_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockCurrentUser = new Mock<ICurrentUserContext>();

            mockCurrentUser.Setup(u => u.UserId).Returns(1);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDbContext.Setup(c => c.GetNextIdAsync("Pivot", It.IsAny<CancellationToken>())).ReturnsAsync(123);
            mockPivots.Setup(c => c.InsertOneAsync(It.IsAny<Pivot>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var handler = new CreatePivotHandler(mockDbContext.Object, mockCurrentUser.Object);
            var request = new CreatePivotRequest { Name = "TestPivot" };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
        }
    }
}
