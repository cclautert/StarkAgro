using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class GetPivotHandlerTests
    {
        private agpDBContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<agpDBContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_PivotHandler" + System.Guid.NewGuid())
                .Options;
            return new agpDBContext(options);
        }

        [Fact]
        public async Task Handle_Returns_Pivot_Response()
        {
            var context = CreateInMemoryContext();
            context.Pivots.Add(new Pivot { Id = 2, Name = "Pivot2" });
            context.SaveChanges();

            var handler = new GetPivotHandler(context);
            var request = new GetPivotRequest { Id = 2 };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(2, result.Id);
            Assert.Equal("Pivot2", result.Name);
        }
    }
}