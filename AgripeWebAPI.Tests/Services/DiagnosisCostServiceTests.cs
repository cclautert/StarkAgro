using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Services.Diagnosis;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Services
{
    public class DiagnosisCostServiceTests
    {
        private static DiagnosisCostService Build(List<PlantDiagnosis> diagnoses)
        {
            var col = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(col, diagnoses);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(col.Object);

            return new DiagnosisCostService(db.Object);
        }

        [Fact]
        public async Task Soma_o_custo_congelado_de_cada_laudo_do_mes()
        {
            var service = Build([
                new PlantDiagnosis { Id = 1, AiCostCents = 3, CreatedAt = DateTime.UtcNow },
                new PlantDiagnosis { Id = 2, AiCostCents = 5, CreatedAt = DateTime.UtcNow },
                new PlantDiagnosis { Id = 3, AiCostCents = 3, CreatedAt = DateTime.UtcNow },
            ]);

            var total = await service.GetCurrentMonthCostCentsAsync(CancellationToken.None);

            Assert.Equal(11, total);
        }

        [Fact]
        public async Task Sem_laudos_cobrados_o_total_e_zero()
        {
            var service = Build([]);

            var total = await service.GetCurrentMonthCostCentsAsync(CancellationToken.None);

            Assert.Equal(0, total);
        }

        [Fact]
        public async Task Laudo_sem_custo_nao_contribui()
        {
            // AiCostCents nulo = a foto nunca chegou a ser classificada (falhou antes) ou é
            // anterior ao rastreamento. O filtro do serviço exclui esses; se algum passar pelo
            // mock (que ignora o filtro), o ?? 0 garante que não quebra a soma.
            var service = Build([
                new PlantDiagnosis { Id = 1, AiCostCents = 4, CreatedAt = DateTime.UtcNow },
                new PlantDiagnosis { Id = 2, AiCostCents = null, CreatedAt = DateTime.UtcNow },
            ]);

            var total = await service.GetCurrentMonthCostCentsAsync(CancellationToken.None);

            Assert.Equal(4, total);
        }
    }
}
