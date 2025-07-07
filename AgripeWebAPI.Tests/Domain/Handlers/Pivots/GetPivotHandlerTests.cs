using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetPivotHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Pivot_Response()
        {
            var pivot = new Pivot { Id = 2, Name = "Pivot2" };
            var pivots = new List<Pivot> { pivot }.AsQueryable();
            var mockSet = new Mock<DbSet<Pivot>>();
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Provider).Returns(pivots.Provider);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Expression).Returns(pivots.Expression);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.ElementType).Returns(pivots.ElementType);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.GetEnumerator()).Returns(pivots.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Pivots).Returns(mockSet.Object);

            var handler = new GetPivotHandler(mockContext.Object);
            var request = new GetPivotRequest { Id = 2 };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(2, result.Id);
            Assert.Equal("Pivot2", result.Name);
        }
    }
}