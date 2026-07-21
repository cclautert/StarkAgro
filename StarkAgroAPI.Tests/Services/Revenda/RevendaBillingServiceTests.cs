using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Services.Revenda;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;
using RevendaEntity = StarkAgroAPI.Models.Entities.Revenda;

namespace StarkAgroAPI.Tests.Services.Revenda
{
    public class RevendaBillingServiceTests
    {
        private static ProducerInvoice Invoice(int userId, int used) =>
            new(userId, null, "Sem plano", 0, 0, used, 0, 0, 0, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        private static (RevendaBillingService svc, Mock<IDiagnosisBillingService> billing) Build(
            List<RevendaEntity>? revendas,
            List<RevendaMembership>? memberships,
            List<User>? users,
            List<DiagnosisPlan>? plans,
            Dictionary<int, int>? usageByClient)
        {
            var revendasCol = new Mock<IMongoCollection<RevendaEntity>>();
            MongoMockHelper.SetupFindList(revendasCol, revendas ?? []);
            var membershipsCol = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(membershipsCol, memberships ?? []);
            var usersCol = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(usersCol, users ?? []);
            var plansCol = new Mock<IMongoCollection<DiagnosisPlan>>();
            MongoMockHelper.SetupFindList(plansCol, plans ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Revendas).Returns(revendasCol.Object);
            db.Setup(d => d.RevendaMemberships).Returns(membershipsCol.Object);
            db.Setup(d => d.Users).Returns(usersCol.Object);
            db.Setup(d => d.DiagnosisPlans).Returns(plansCol.Object);

            var billing = new Mock<IDiagnosisBillingService>();
            billing.Setup(b => b.GetProducerInvoiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) => Invoice(id, usageByClient?.GetValueOrDefault(id) ?? 0));

            // Serviço de assentos real sobre o mesmo mock: a contagem sai das mesmas memberships,
            // então a fatura e o teto não podem divergir.
            var seats = new RevendaSeatService(db.Object);

            return (new RevendaBillingService(db.Object, billing.Object, seats), billing);
        }

        [Fact]
        public async Task ComPlano_CalculaPoolEExcedente()
        {
            var (svc, _) = Build(
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = 1 }],
                memberships:
                [
                    new RevendaMembership { Id = 1, RevendaId = 7, MemberUserId = 3, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active },
                    new RevendaMembership { Id = 2, RevendaId = 7, MemberUserId = 4, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active }
                ],
                users: [new User { Id = 3, Name = "A" }, new User { Id = 4, Name = "B" }],
                plans: [new DiagnosisPlan { Id = 1, Name = "Pro", MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500 }],
                usageByClient: new() { [3] = 8, [4] = 7 });

            var inv = await svc.GetRevendaInvoiceAsync(7, CancellationToken.None);

            Assert.NotNull(inv);
            Assert.Equal(15, inv!.UsedReports);      // 8 + 7
            Assert.Equal(5, inv.OverageReports);     // max(0, 15 - 10)
            Assert.Equal(12400, inv.TotalCents);     // 9900 + 5*500
            Assert.Equal(2, inv.Clients.Count);
            Assert.Equal("Pro", inv.PlanName);
        }

        [Fact]
        public async Task DentroDaCota_SemExcedente()
        {
            var (svc, _) = Build(
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = 1 }],
                memberships: [new RevendaMembership { Id = 1, RevendaId = 7, MemberUserId = 3, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active }],
                users: [new User { Id = 3, Name = "A" }],
                plans: [new DiagnosisPlan { Id = 1, Name = "Pro", MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500 }],
                usageByClient: new() { [3] = 4 });

            var inv = await svc.GetRevendaInvoiceAsync(7, CancellationToken.None);

            Assert.Equal(4, inv!.UsedReports);
            Assert.Equal(0, inv.OverageReports);
            Assert.Equal(9900, inv.TotalCents); // só mensalidade
        }

        [Fact]
        public async Task SemPlano_FaturaZeroMasMostraConsumo()
        {
            var (svc, _) = Build(
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = null }],
                memberships: [new RevendaMembership { Id = 1, RevendaId = 7, MemberUserId = 3, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active }],
                users: [new User { Id = 3, Name = "A" }],
                plans: [],
                usageByClient: new() { [3] = 9 });

            var inv = await svc.GetRevendaInvoiceAsync(7, CancellationToken.None);

            Assert.Equal(9, inv!.UsedReports);
            Assert.Equal(0, inv.TotalCents);
            Assert.Equal("Sem plano", inv.PlanName);
        }

        [Fact]
        public async Task SemClientesAtivos_Zero()
        {
            var (svc, _) = Build(
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = 1 }],
                memberships: [],
                users: [],
                plans: [new DiagnosisPlan { Id = 1, Name = "Pro", MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500 }],
                usageByClient: new());

            var inv = await svc.GetRevendaInvoiceAsync(7, CancellationToken.None);

            Assert.Equal(0, inv!.UsedReports);
            Assert.Empty(inv.Clients);
            Assert.Equal(9900, inv.TotalCents); // mensalidade mesmo sem consumo
        }

        [Fact]
        public async Task AssentosAlemDoIncluso_EntramNoTotal()
        {
            var (svc, _) = Build(
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = 1 }],
                memberships:
                [
                    new RevendaMembership { Id = 1, RevendaId = 7, MemberUserId = 3, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active },
                    new RevendaMembership { Id = 2, RevendaId = 7, MemberUserId = 4, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active },
                    new RevendaMembership { Id = 3, RevendaId = 7, MemberUserId = 5, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active }
                ],
                users: [new User { Id = 3 }, new User { Id = 4 }, new User { Id = 5 }],
                plans:
                [
                    new DiagnosisPlan
                    {
                        Id = 1, Name = "Pro", MonthlyPriceCents = 9900,
                        IncludedReportsPerMonth = 100, OveragePriceCents = 500,
                        IncludedMembers = 2, MemberOveragePriceCents = 1500
                    }
                ],
                usageByClient: new() { [3] = 1 });

            var inv = await svc.GetRevendaInvoiceAsync(7, CancellationToken.None);

            Assert.Equal(3, inv!.SeatsUsed);
            Assert.Equal(2, inv.IncludedMembers);
            Assert.Equal(1, inv.SeatOverage);           // max(0, 3 - 2)
            Assert.Equal(1500, inv.SeatOverageCents);
            Assert.Equal(0, inv.OverageReports);        // 1 laudo, 100 inclusos
            Assert.Equal(11400, inv.TotalCents);        // 9900 + 1500
        }

        [Fact]
        public async Task PlanoLegadoSemCamposDeAssento_MantemFaturaDeAntes()
        {
            // Documento gravado antes de assentos existirem desserializa com 0 nos campos novos:
            // 0 incluso × 0 centavos = nada a cobrar por assento.
            var (svc, _) = Build(
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul", DiagnosisPlanId = 1 }],
                memberships:
                [
                    new RevendaMembership { Id = 1, RevendaId = 7, MemberUserId = 3, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active },
                    new RevendaMembership { Id = 2, RevendaId = 7, MemberUserId = 4, MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active }
                ],
                users: [new User { Id = 3 }, new User { Id = 4 }],
                plans: [new DiagnosisPlan { Id = 1, Name = "Antigo", MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500 }],
                usageByClient: new() { [3] = 2 });

            var inv = await svc.GetRevendaInvoiceAsync(7, CancellationToken.None);

            Assert.Equal(2, inv!.SeatsUsed);
            Assert.Equal(2, inv.SeatOverage);        // sem incluso, tudo é excedente...
            Assert.Equal(0, inv.SeatOverageCents);   // ...mas a 0 centavos
            Assert.Equal(9900, inv.TotalCents);
        }

        [Fact]
        public async Task RevendaInexistente_RetornaNull()
        {
            var (svc, _) = Build(revendas: [], memberships: null, users: null, plans: null, usageByClient: null);

            var inv = await svc.GetRevendaInvoiceAsync(99, CancellationToken.None);

            Assert.Null(inv);
        }
    }
}
