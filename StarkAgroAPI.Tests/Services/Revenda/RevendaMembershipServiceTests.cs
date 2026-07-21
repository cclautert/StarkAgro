using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Revenda;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services.Revenda
{
    public class RevendaMembershipServiceTests
    {
        private static Mock<agpDBContext> Db(List<RevendaMembership> memberships)
        {
            var col = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(col, memberships);
            var db = new Mock<agpDBContext>();
            db.Setup(d => d.RevendaMemberships).Returns(col.Object);
            return db;
        }

        [Fact]
        public async Task GetManagedRevendaId_RetornaRevendaDoGestorAtivo()
        {
            var db = Db([new RevendaMembership
            {
                Id = 1, RevendaId = 7, MemberUserId = 42,
                MemberRole = RevendaMemberRole.Manager, Status = RevendaMembershipStatus.Active
            }]);
            var svc = new RevendaMembershipService(db.Object);

            var result = await svc.GetManagedRevendaIdAsync(42, CancellationToken.None);

            Assert.Equal(7, result);
        }

        [Fact]
        public async Task GetManagedRevendaId_SemVinculo_RetornaNull()
        {
            var db = Db([]);
            var svc = new RevendaMembershipService(db.Object);

            var result = await svc.GetManagedRevendaIdAsync(42, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
