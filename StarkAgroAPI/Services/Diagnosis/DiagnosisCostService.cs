using StarkAgroAPI.Models;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Diagnosis
{
    /// <summary>
    /// Quanto a plataforma gastou em IA (chamadas ao classificador) num período.
    /// <para>
    /// A margem do laudo é ~100%, mas o custo precisa ser <b>visível</b>: é o que impede a
    /// surpresa no fim do mês e o que dá base para precificar o plano. Soma o custo já
    /// congelado em cada laudo (<c>AiCostCents</c>), então reflete o preço do dia de cada
    /// chamada, não o preço de hoje.
    /// </para>
    /// </summary>
    public interface IDiagnosisCostService
    {
        /// <summary>Custo total de IA (em centavos) no mês corrente, plataforma inteira.</summary>
        Task<int> GetCurrentMonthCostCentsAsync(CancellationToken cancellationToken);
    }

    public class DiagnosisCostService : IDiagnosisCostService
    {
        private readonly agpDBContext _dbContext;

        public DiagnosisCostService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<int> GetCurrentMonthCostCentsAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonth = monthStart.AddMonths(1);

            // Só laudos que chegaram a ser classificados têm custo (AiCostCents != null).
            var billed = await _dbContext.PlantDiagnoses
                .Find(d => d.CreatedAt >= monthStart
                           && d.CreatedAt < nextMonth
                           && d.AiCostCents != null)
                .ToListAsync(cancellationToken);

            return billed.Sum(d => d.AiCostCents ?? 0);
        }
    }
}
