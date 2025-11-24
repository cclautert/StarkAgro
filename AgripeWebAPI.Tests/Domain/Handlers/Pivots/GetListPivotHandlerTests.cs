using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetListPivotHandlerTests
    {
        private agpDBContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<agpDBContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_ListPivotHandler" + System.Guid.NewGuid())
                .Options;
            return new agpDBContext(options);
        }

        [Fact]
        public async Task Handle_Returns_Pivots_For_UserId()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userId = 5;
            context.Pivots.AddRange(
                new Pivot { Id = 1, Name = "Pivot A", UserId = userId },
                new Pivot { Id = 2, Name = "Pivot B", UserId = userId },
                new Pivot { Id = 3, Name = "Other", UserId = 99 }
            );
            context.SaveChanges();

            var handler = new GetListPivotHandler(context);
            var request = new GetListPivotByUserIdRequest { UserId = userId };

            // Act
            var result = await handler.Handle(request, default);
            var list = result.ToList(); // Materialize results

            // Assert
            Assert.NotNull(list);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, p => p.Id == 1 && p.Name == "Pivot A");
            Assert.Contains(list, p => p.Id == 2 && p.Name == "Pivot B");
        }

        [Fact]
        public async Task Handle_Returns_EmptyList_When_No_Pivots_For_UserId()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var userId = 100;
            context.Pivots.Add(new Pivot { Id = 1, Name = "Pivot A", UserId = 1 });
            context.SaveChanges();

            var handler = new GetListPivotHandler(context);
            var request = new GetListPivotByUserIdRequest { UserId = userId };

            // Act
            var result = await handler.Handle(request, default);
            var list = result.ToList(); // Materialize results

            // Assert
            Assert.NotNull(list);
            Assert.Empty(list);
        }
    }
}