using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class CreatePivotHandlerTests
    {
        [Fact]
        public async Task Handle_Creates_Pivot_And_Returns_Response()
        {
            // Arrange
            var pivots = new List<Pivot>().AsQueryable();
            var mockSet = new Mock<DbSet<Pivot>>();

            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Provider).Returns(pivots.Provider);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Expression).Returns(pivots.Expression);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.ElementType).Returns(pivots.ElementType);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.GetEnumerator()).Returns(pivots.GetEnumerator());
            mockSet.Setup(m => m.Add(It.IsAny<Pivot>())).Callback<Pivot>(p => p.Id = 123).Returns((Pivot p) => new Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Pivot>(null));

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Pivots).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChanges()).Returns(1);

            var handler = new CreatePivotHandler(mockContext.Object);
            var request = new CreatePivotRequest { UserId = 1, Name = "TestPivot" };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(123, result.Id);
        }
    }
}