using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Diagnosis;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Revenda
{
    /// <param name="UsedReports">Laudos que o cliente enviou no mês (recusados contam — pagaram o classificador).</param>
    public record RevendaInvoiceClientLine(int ClientUserId, string? ClientName, string? ClientEmail, int UsedReports);

    /// <summary>
    /// Fatura mensal da revenda no modelo <b>pool</b>: um plano para toda a base. <see cref="UsedReports"/>
    /// é a soma dos laudos de todos os clientes ativos; o excedente é calculado sobre esse total.
    /// <para>
    /// Duas linhas de excedente, dois eixos: <b>laudo</b> é custo (cada foto é chamada paga de IA) e
    /// <b>assento</b> é comercial (tamanho da carteira atendida).
    /// </para>
    /// </summary>
    public record RevendaInvoice(
        int RevendaId,
        string RevendaName,
        int? PlanId,
        string PlanName,
        int MonthlyPriceCents,
        int IncludedReports,
        int UsedReports,
        int OverageReports,
        int OveragePriceCents,
        int SeatsUsed,
        int IncludedMembers,
        int SeatOverage,
        int SeatOveragePriceCents,
        int SeatOverageCents,
        int TotalCents,
        IReadOnlyList<RevendaInvoiceClientLine> Clients,
        DateTime PeriodStart,
        DateTime PeriodEnd);

    /// <summary>
    /// Calcula e <b>mostra</b> a fatura da revenda (mensalidade do plano da revenda + excedente sobre
    /// o consumo agregado dos clientes). Não cobra — o gateway é etapa futura. Reusa
    /// <see cref="IDiagnosisBillingService"/> para o consumo de cada cliente.
    /// </summary>
    public interface IRevendaBillingService
    {
        /// <summary>Fatura da revenda, ou null se a revenda não existe.</summary>
        Task<RevendaInvoice?> GetRevendaInvoiceAsync(int revendaId, CancellationToken cancellationToken);
    }

    public class RevendaBillingService : IRevendaBillingService
    {
        private readonly agpDBContext _dbContext;
        private readonly IDiagnosisBillingService _billing;
        private readonly IRevendaSeatService _seats;

        public RevendaBillingService(agpDBContext dbContext, IDiagnosisBillingService billing, IRevendaSeatService seats)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _billing = billing ?? throw new ArgumentNullException(nameof(billing));
            _seats = seats ?? throw new ArgumentNullException(nameof(seats));
        }

        public async Task<RevendaInvoice?> GetRevendaInvoiceAsync(int revendaId, CancellationToken cancellationToken)
        {
            var revenda = await _dbContext.Revendas
                .Find(r => r.Id == revendaId)
                .FirstOrDefaultAsync(cancellationToken);
            if (revenda is null) return null;

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonth = monthStart.AddMonths(1);

            // Só clientes ativos e já com conta têm consumo a faturar.
            var links = await _dbContext.RevendaMemberships
                .Find(m => m.RevendaId == revendaId
                           && m.MemberRole == RevendaMemberRole.Client
                           && m.Status == RevendaMembershipStatus.Active
                           && m.MemberUserId != null)
                .ToListAsync(cancellationToken);

            var clientIds = links.Select(m => m.MemberUserId!.Value).Distinct().ToList();
            var clients = clientIds.Count == 0
                ? []
                : await _dbContext.Users.Find(u => clientIds.Contains(u.Id)).ToListAsync(cancellationToken);
            var byId = clients.ToDictionary(u => u.Id);

            var lines = new List<RevendaInvoiceClientLine>();
            var totalUsed = 0;
            foreach (var clientId in clientIds)
            {
                // GetProducerInvoiceAsync devolve UsedReports mesmo sem plano (produtor de revenda
                // fica com DiagnosisPlanId = null) — é só o consumo do mês que interessa aqui.
                var invoice = await _billing.GetProducerInvoiceAsync(clientId, cancellationToken);
                byId.TryGetValue(clientId, out var user);
                lines.Add(new RevendaInvoiceClientLine(clientId, user?.Name, user?.Email, invoice.UsedReports));
                totalUsed += invoice.UsedReports;
            }

            // Assentos contam a base inteira (inclusive convite pendente), não só quem consumiu no mês.
            var seats = await _seats.GetAsync(revendaId, cancellationToken);

            var plan = revenda.DiagnosisPlanId is int planId
                ? await _dbContext.DiagnosisPlans.Find(p => p.Id == planId).FirstOrDefaultAsync(cancellationToken)
                : null;

            if (plan is null)
            {
                // Sem plano: nada a faturar; só mostra o consumo agregado e o tamanho da base.
                return new RevendaInvoice(
                    revendaId, revenda.Name, null, "Sem plano",
                    0, 0, totalUsed, 0, 0,
                    seats.Used, 0, 0, 0, 0,
                    0, lines, monthStart, nextMonth);
            }

            var overage = Math.Max(0, totalUsed - plan.IncludedReportsPerMonth);
            var reportOverageCents = overage * plan.OveragePriceCents;
            var seatOverageCents = seats.Overage * plan.MemberOveragePriceCents;
            var total = plan.MonthlyPriceCents + reportOverageCents + seatOverageCents;

            return new RevendaInvoice(
                revendaId, revenda.Name, plan.Id, plan.Name,
                plan.MonthlyPriceCents, plan.IncludedReportsPerMonth, totalUsed, overage,
                plan.OveragePriceCents,
                seats.Used, plan.IncludedMembers, seats.Overage, plan.MemberOveragePriceCents, seatOverageCents,
                total, lines, monthStart, nextMonth);
        }
    }
}
