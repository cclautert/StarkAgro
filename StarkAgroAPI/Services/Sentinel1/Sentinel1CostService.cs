using StarkAgroAPI.Models;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Sentinel1
{
    /// <summary>
    /// Gasto de Sentinel-1 (Processing Units) no mês. Espelho puro de <c>NdviCostService</c>: soma o
    /// custo já congelado em cada <c>Sentinel1Reading.Sentinel1CostCents</c>. Exposto em <c>/admin/ia</c>.
    /// </summary>
    public interface ISentinel1CostService
    {
        Task<int> GetCurrentMonthCostCentsAsync(CancellationToken cancellationToken);
    }

    public class Sentinel1CostService : ISentinel1CostService
    {
        private readonly agpDBContext _dbContext;

        public Sentinel1CostService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<int> GetCurrentMonthCostCentsAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonth = monthStart.AddMonths(1);

            var readings = await _dbContext.Sentinel1Readings
                .Find(r => r.CreatedAt >= monthStart && r.CreatedAt < nextMonth)
                .ToListAsync(cancellationToken);

            return readings.Sum(r => r.Sentinel1CostCents);
        }
    }
}
