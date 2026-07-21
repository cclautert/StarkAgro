using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Revenda;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;
using RevendaEntity = StarkAgroAPI.Models.Entities.Revenda;

namespace StarkAgroAPI.Tests.Services.Revenda
{
    public class RevendaSeatServiceTests
    {
        private static readonly DateTime Future = DateTime.UtcNow.AddDays(3);
        private static readonly DateTime Past = DateTime.UtcNow.AddDays(-3);

        private static RevendaSeatService Build(
            List<RevendaMembership> memberships,
            List<RevendaEntity>? revendas = null,
            List<DiagnosisPlan>? plans = null)
        {
            var membershipsCol = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(membershipsCol, memberships);
            var revendasCol = new Mock<IMongoCollection<RevendaEntity>>();
            MongoMockHelper.SetupFindList(revendasCol, revendas ?? [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = 1 }]);
            var plansCol = new Mock<IMongoCollection<DiagnosisPlan>>();
            MongoMockHelper.SetupFindList(plansCol, plans ?? [new DiagnosisPlan { Id = 1, IncludedMembers = 2, MaxMembers = 3 }]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.RevendaMemberships).Returns(membershipsCol.Object);
            db.Setup(d => d.Revendas).Returns(revendasCol.Object);
            db.Setup(d => d.DiagnosisPlans).Returns(plansCol.Object);

            return new RevendaSeatService(db.Object);
        }

        private static RevendaMembership Client(int id, string status, DateTime? expires = null) => new()
        {
            Id = id,
            RevendaId = 7,
            MemberUserId = id,
            MemberRole = RevendaMemberRole.Client,
            Status = status,
            InviteExpiresAt = expires ?? Future
        };

        [Fact]
        public async Task ConvitePendenteReservaAssento()
        {
            var svc = Build([Client(1, RevendaMembershipStatus.Active), Client(2, RevendaMembershipStatus.Pending)]);

            var seats = await svc.GetAsync(7, CancellationToken.None);

            Assert.Equal(2, seats.Used);
        }

        [Fact]
        public async Task ConvitePendenteVencido_NaoOcupaAssento()
        {
            // O status só vira Expired quando alguém tenta aceitar; sem o filtro de data o convite
            // morto seguraria a vaga para sempre.
            var svc = Build([Client(1, RevendaMembershipStatus.Active), Client(2, RevendaMembershipStatus.Pending, Past)]);

            var seats = await svc.GetAsync(7, CancellationToken.None);

            Assert.Equal(1, seats.Used);
        }

        [Fact]
        public async Task GestorEAgronomo_NaoOcupamAssento()
        {
            var svc = Build(
            [
                Client(1, RevendaMembershipStatus.Active),
                new RevendaMembership { Id = 2, RevendaId = 7, MemberUserId = 2, MemberRole = RevendaMemberRole.Manager, Status = RevendaMembershipStatus.Active, InviteExpiresAt = Future },
                new RevendaMembership { Id = 3, RevendaId = 7, MemberUserId = 3, MemberRole = RevendaMemberRole.Agronomist, Status = RevendaMembershipStatus.Active, InviteExpiresAt = Future }
            ]);

            var seats = await svc.GetAsync(7, CancellationToken.None);

            Assert.Equal(1, seats.Used);
        }

        [Fact]
        public async Task RevogadoOuRecusado_NaoOcupamAssento()
        {
            var svc = Build(
            [
                Client(1, RevendaMembershipStatus.Active),
                Client(2, RevendaMembershipStatus.Revoked),
                Client(3, RevendaMembershipStatus.Declined),
                Client(4, RevendaMembershipStatus.Expired)
            ]);

            var seats = await svc.GetAsync(7, CancellationToken.None);

            Assert.Equal(1, seats.Used);
        }

        [Fact]
        public async Task NoTeto_FicaCheio()
        {
            var svc = Build([Client(1, RevendaMembershipStatus.Active), Client(2, RevendaMembershipStatus.Active), Client(3, RevendaMembershipStatus.Active)]);

            var seats = await svc.GetAsync(7, CancellationToken.None);

            Assert.True(seats.IsFull);      // 3 usados, teto 3
            Assert.Equal(1, seats.Overage); // 3 usados, 2 inclusos
        }

        [Fact]
        public async Task MaxZero_EIlimitado()
        {
            var svc = Build(
                [Client(1, RevendaMembershipStatus.Active), Client(2, RevendaMembershipStatus.Active)],
                plans: [new DiagnosisPlan { Id = 1, IncludedMembers = 1, MaxMembers = 0 }]);

            var seats = await svc.GetAsync(7, CancellationToken.None);

            Assert.True(seats.IsUnlimited);
            Assert.False(seats.IsFull);
            Assert.Equal(1, seats.Overage);
        }

        [Fact]
        public async Task RevendaSemPlano_SemTetoESemIncluso()
        {
            var svc = Build(
                [Client(1, RevendaMembershipStatus.Active)],
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = null }],
                plans: []);

            var seats = await svc.GetAsync(7, CancellationToken.None);

            Assert.Equal(1, seats.Used);
            Assert.Equal(0, seats.Included);
            Assert.True(seats.IsUnlimited);
        }
    }
}
