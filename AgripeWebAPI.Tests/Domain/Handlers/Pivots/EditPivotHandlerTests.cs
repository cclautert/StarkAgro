using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class EditPivotHandlerTests
    {
        [Fact]
        public async Task Handle_Updates_Pivot_And_Returns_Response()
        {
            var pivot = new Pivot { Id = 1, Name = "OldName" };
            var pivots = new List<Pivot> { pivot }.AsQueryable();
            var mockSet = new Mock<DbSet<Pivot>>();
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Provider).Returns(pivots.Provider);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Expression).Returns(pivots.Expression);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.ElementType).Returns(pivots.ElementType);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.GetEnumerator()).Returns(pivots.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Pivots).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChanges()).Returns(1);

            var handler = new EditPivotHandler(mockContext.Object);
            var request = new EditPivotRequest { Id = 1, Name = "NewName" };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("NewName", pivot.Name);
        }
    }
}