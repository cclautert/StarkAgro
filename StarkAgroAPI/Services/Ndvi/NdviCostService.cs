using StarkAgroAPI.Models;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Quanto a plataforma gastou em NDVI (Processing Units da CDSE) num período.
    /// <para>
    /// Espelha <c>DiagnosisCostService</c>: soma o custo já <b>congelado</b> em cada
    /// <c>NdviReading.NdviCostCents</c>, então reflete o preço do dia de cada busca, não o de
    /// hoje. Visão de plataforma (admin) — é o que dá base para o teto de orçamento de PU.
    /// </para>
    /// </summary>
    public interface INdviCostService
    {
        /// <summary>Custo total de NDVI (em centavos) no mês corrente, plataforma inteira.</summary>
        Task<int> GetCurrentMonthCostCentsAsync(CancellationToken cancellationToken);
    }

    public class NdviCostService : INdviCostService
    {
        private readonly agpDBContext _dbContext;

        public NdviCostService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<int> GetCurrentMonthCostCentsAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonth = monthStart.AddMonths(1);

            var readings = await _dbContext.NdviReadings
                .Find(r => r.CreatedAt >= monthStart && r.CreatedAt < nextMonth)
                .ToListAsync(cancellationToken);

            return readings.Sum(r => r.NdviCostCents);
        }
    }
}
