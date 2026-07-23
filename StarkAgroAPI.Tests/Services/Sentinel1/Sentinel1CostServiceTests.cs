using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Sentinel1;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services.Sentinel1
{
    public class Sentinel1CostServiceTests
    {
        private static Sentinel1CostService Build(List<Sentinel1Reading> readings)
        {
            var col = new Mock<IMongoCollection<Sentinel1Reading>>();
            MongoMockHelper.SetupFindList(col, readings);
            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Sentinel1Readings).Returns(col.Object);
            return new Sentinel1CostService(db.Object);
        }

        [Fact]
        public async Task GetCurrentMonthCost_SomaOCustoCongelado()
        {
            var now = DateTime.UtcNow;
            var svc = Build(
            [
                new Sentinel1Reading { Id = 1, Sentinel1CostCents = 1, CreatedAt = now },
                new Sentinel1Reading { Id = 2, Sentinel1CostCents = 2, CreatedAt = now }
            ]);

            Assert.Equal(3, await svc.GetCurrentMonthCostCentsAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetCurrentMonthCost_SemLeituras_Zero()
        {
            var svc = Build([]);
            Assert.Equal(0, await svc.GetCurrentMonthCostCentsAsync(CancellationToken.None));
        }
    }
}
