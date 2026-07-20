using StarkAgroAPI.Models;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Diagnosis
{
    /// <param name="PlanName">Nome do plano, ou "Sem plano" quando o produtor não tem um.</param>
    /// <param name="UsedReports">Laudos enviados no mês (recusados contam — a chamada foi paga).</param>
    /// <param name="OverageReports">Laudos além do incluso no plano.</param>
    /// <param name="TotalCents">Mensalidade + excedente, em centavos.</param>
    public record ProducerInvoice(
        int UserId,
        int? PlanId,
        string PlanName,
        int MonthlyPriceCents,
        int IncludedReports,
        int UsedReports,
        int OverageReports,
        int OveragePriceCents,
        int TotalCents,
        DateTime PeriodStart,
        DateTime PeriodEnd);

    /// <summary>
    /// Fatura mensal de um produtor: mensalidade do plano + excedente. Só <b>calcula e mostra</b>
    /// o que é devido — não cobra de fato (o gateway de pagamento é outra etapa). Os preços vêm
    /// do <see cref="Models.Entities.DiagnosisPlan"/>, editável em <c>/admin</c>.
    /// </summary>
    public interface IDiagnosisBillingService
    {
        Task<ProducerInvoice> GetProducerInvoiceAsync(int userId, CancellationToken cancellationToken);
    }

    public class DiagnosisBillingService : IDiagnosisBillingService
    {
        private readonly agpDBContext _dbContext;

        public DiagnosisBillingService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<ProducerInvoice> GetProducerInvoiceAsync(int userId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonth = monthStart.AddMonths(1);

            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);

            // Conta o que foi ENVIADO no mês — inclusive recusados, que também pagaram o
            // classificador. Mesmo critério da cota, para fatura e cota não divergirem.
            var used = await _dbContext.PlantDiagnoses
                .Find(d => d.UserId == userId && d.CreatedAt >= monthStart && d.CreatedAt < nextMonth)
                .ToListAsync(cancellationToken);
            var usedCount = used.Count;

            var plan = user?.DiagnosisPlanId is int planId
                ? await _dbContext.DiagnosisPlans.Find(p => p.Id == planId).FirstOrDefaultAsync(cancellationToken)
                : null;

            if (plan is null)
            {
                // Sem plano: nada a faturar. A cota ainda pode limitar pelo padrão da plataforma,
                // mas dinheiro devido é zero.
                return new ProducerInvoice(
                    userId, null, "Sem plano", 0, 0, usedCount, 0, 0, 0, monthStart, nextMonth);
            }

            var overage = Math.Max(0, usedCount - plan.IncludedReportsPerMonth);
            var total = plan.MonthlyPriceCents + overage * plan.OveragePriceCents;

            return new ProducerInvoice(
                userId,
                plan.Id,
                plan.Name,
                plan.MonthlyPriceCents,
                plan.IncludedReportsPerMonth,
                usedCount,
                overage,
                plan.OveragePriceCents,
                total,
                monthStart,
                nextMonth);
        }
    }
}
