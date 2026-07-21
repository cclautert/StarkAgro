using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class NdviCostServiceTests
    {
        private static NdviCostService Build(List<NdviReading> readings)
        {
            var col = new Mock<IMongoCollection<NdviReading>>();
            MongoMockHelper.SetupFindList(col, readings);
            var db = new Mock<agpDBContext>();
            db.Setup(d => d.NdviReadings).Returns(col.Object);
            return new NdviCostService(db.Object);
        }

        [Fact]
        public async Task GetCurrentMonthCost_SumsFrozenCostCents()
        {
            var now = DateTime.UtcNow;
            var svc = Build(
            [
                new NdviReading { Id = 1, NdviCostCents = 2, CreatedAt = now },
                new NdviReading { Id = 2, NdviCostCents = 3, CreatedAt = now },
                new NdviReading { Id = 3, NdviCostCents = 5, CreatedAt = now }
            ]);

            var total = await svc.GetCurrentMonthCostCentsAsync(CancellationToken.None);

            Assert.Equal(10, total);
        }

        [Fact]
        public async Task GetCurrentMonthCost_NoReadings_IsZero()
        {
            var svc = Build([]);

            Assert.Equal(0, await svc.GetCurrentMonthCostCentsAsync(CancellationToken.None));
        }
    }
}
