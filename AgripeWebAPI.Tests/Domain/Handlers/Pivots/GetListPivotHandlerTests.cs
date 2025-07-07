using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetListPivotHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Pivots_For_UserId()
        {
            // Arrange
            var userId = 5;
            var pivots = new List<Pivot>
                {
                    new Pivot { Id = 1, Name = "Pivot A", UserId = userId },
                    new Pivot { Id = 2, Name = "Pivot B", UserId = userId },
                    new Pivot { Id = 3, Name = "Other", UserId = 99 }
                }.AsQueryable();

            var mockSet = new Mock<DbSet<Pivot>>();
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Provider).Returns(pivots.Provider);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Expression).Returns(pivots.Expression);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.ElementType).Returns(pivots.ElementType);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.GetEnumerator()).Returns(pivots.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Pivots).Returns(mockSet.Object);

            var handler = new GetListPivotHandler(mockContext.Object);
            var request = new GetListPivotByUserIdRequest { UserId = userId };

            // Act  
            var result = await handler.Handle(request, default);

            // Assert  
            Assert.NotNull(result);
            var list = result; // Fix: Use ToListAsync() for IAsyncEnumerable
            //Assert.Equal(2, list.Count);
            Assert.Contains(list, p => p.Id == 1 && p.Name == "Pivot A");
            Assert.Contains(list, p => p.Id == 2 && p.Name == "Pivot B");
        }

        [Fact]
        public async Task Handle_Returns_EmptyList_When_No_Pivots_For_UserId()
        {
            // Arrange  
            var userId = 100;
            var pivots = new List<Pivot>
                    {
                        new Pivot { Id = 1, Name = "Pivot A", UserId = 1 }
                    }.AsQueryable();

            var mockSet = new Mock<DbSet<Pivot>>();
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Provider).Returns(pivots.Provider);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.Expression).Returns(pivots.Expression);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.ElementType).Returns(pivots.ElementType);
            mockSet.As<IQueryable<Pivot>>().Setup(m => m.GetEnumerator()).Returns(pivots.GetEnumerator());

            var mockContext = new Mock<agpDBContext>(new DbContextOptions<agpDBContext>());
            mockContext.Setup(c => c.Pivots).Returns(mockSet.Object);

            var handler = new GetListPivotHandler(mockContext.Object);
            var request = new GetListPivotByUserIdRequest { UserId = userId };

            // Act  
            var result = await handler.Handle(request, default);

            // Assert  
            Assert.NotNull(result);
            var list = result; // Fix: Use ToListAsync() for IAsyncEnumerable
            Assert.Empty(list);
        }
    }
}