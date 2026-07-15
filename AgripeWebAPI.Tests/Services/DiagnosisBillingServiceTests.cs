using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Services.Diagnosis;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Services
{
    public class DiagnosisBillingServiceTests
    {
        private const int UserId = 3;

        private static DiagnosisBillingService Build(User? user, DiagnosisPlan? plan, int usedThisMonth)
        {
            var users = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(users, user is null ? [] : [user]);

            var plans = new Mock<IMongoCollection<DiagnosisPlan>>();
            MongoMockHelper.SetupFindList(plans, plan is null ? [] : [plan]);

            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(diagnoses,
                Enumerable.Range(1, usedThisMonth)
                    .Select(i => new PlantDiagnosis { Id = i, UserId = UserId, CreatedAt = DateTime.UtcNow })
                    .ToList());

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Users).Returns(users.Object);
            db.Setup(d => d.DiagnosisPlans).Returns(plans.Object);
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);

            return new DiagnosisBillingService(db.Object);
        }

        [Fact]
        public async Task Produtor_sem_plano_nao_fatura_nada()
        {
            var service = Build(new User { Id = UserId, DiagnosisPlanId = null }, plan: null, usedThisMonth: 40);

            var invoice = await service.GetProducerInvoiceAsync(UserId, CancellationToken.None);

            Assert.Null(invoice.PlanId);
            Assert.Equal("Sem plano", invoice.PlanName);
            Assert.Equal(0, invoice.TotalCents);
            Assert.Equal(40, invoice.UsedReports); // o consumo continua visível
        }

        [Fact]
        public async Task Dentro_do_incluso_cobra_so_a_mensalidade()
        {
            var plan = new DiagnosisPlan
            {
                Id = 2, Name = "Básico",
                MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500
            };
            var service = Build(new User { Id = UserId, DiagnosisPlanId = 2 }, plan, usedThisMonth: 7);

            var invoice = await service.GetProducerInvoiceAsync(UserId, CancellationToken.None);

            Assert.Equal("Básico", invoice.PlanName);
            Assert.Equal(0, invoice.OverageReports);
            Assert.Equal(9900, invoice.TotalCents); // só a mensalidade
        }

        [Fact]
        public async Task Excedente_soma_a_mensalidade_o_preco_por_laudo_extra()
        {
            var plan = new DiagnosisPlan
            {
                Id = 2, Name = "Básico",
                MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500
            };
            // 13 usados, 10 inclusos → 3 de excedente × 500 = 1500, + 9900 = 11400.
            var service = Build(new User { Id = UserId, DiagnosisPlanId = 2 }, plan, usedThisMonth: 13);

            var invoice = await service.GetProducerInvoiceAsync(UserId, CancellationToken.None);

            Assert.Equal(3, invoice.OverageReports);
            Assert.Equal(11400, invoice.TotalCents);
        }

        [Fact]
        public async Task Exatamente_no_limite_nao_gera_excedente()
        {
            var plan = new DiagnosisPlan
            {
                Id = 2, Name = "Básico",
                MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500
            };
            var service = Build(new User { Id = UserId, DiagnosisPlanId = 2 }, plan, usedThisMonth: 10);

            var invoice = await service.GetProducerInvoiceAsync(UserId, CancellationToken.None);

            Assert.Equal(0, invoice.OverageReports);
            Assert.Equal(9900, invoice.TotalCents);
        }

        [Fact]
        public async Task Plano_apontado_mas_inexistente_trata_como_sem_plano()
        {
            // O plano foi apagado mas o usuário ainda aponta para ele: não pode explodir nem
            // inventar preço — cai em "sem plano", fatura zero.
            var service = Build(new User { Id = UserId, DiagnosisPlanId = 99 }, plan: null, usedThisMonth: 5);

            var invoice = await service.GetProducerInvoiceAsync(UserId, CancellationToken.None);

            Assert.Equal("Sem plano", invoice.PlanName);
            Assert.Equal(0, invoice.TotalCents);
        }
    }
}
