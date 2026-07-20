using StarkAgroAPI.Services.Revenda;

namespace StarkAgroAPI.Domain.Commands.Responses.Revenda
{
    public class RevendaBillingClientLine
    {
        public int ClientUserId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientEmail { get; set; }
        public int UsedReports { get; set; }
    }

    public class RevendaBillingResponse
    {
        public int RevendaId { get; set; }
        public string RevendaName { get; set; } = string.Empty;
        public int? PlanId { get; set; }
        public string PlanName { get; set; } = "Sem plano";
        public int MonthlyPriceCents { get; set; }
        public int IncludedReports { get; set; }
        public int UsedReports { get; set; }
        public int OverageReports { get; set; }
        public int OveragePriceCents { get; set; }
        public int TotalCents { get; set; }
        public List<RevendaBillingClientLine> Clients { get; set; } = [];
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public static RevendaBillingResponse From(RevendaInvoice inv) => new()
        {
            RevendaId = inv.RevendaId,
            RevendaName = inv.RevendaName,
            PlanId = inv.PlanId,
            PlanName = inv.PlanName,
            MonthlyPriceCents = inv.MonthlyPriceCents,
            IncludedReports = inv.IncludedReports,
            UsedReports = inv.UsedReports,
            OverageReports = inv.OverageReports,
            OveragePriceCents = inv.OveragePriceCents,
            TotalCents = inv.TotalCents,
            Clients = inv.Clients.Select(c => new RevendaBillingClientLine
            {
                ClientUserId = c.ClientUserId,
                ClientName = c.ClientName,
                ClientEmail = c.ClientEmail,
                UsedReports = c.UsedReports
            }).ToList(),
            PeriodStart = inv.PeriodStart,
            PeriodEnd = inv.PeriodEnd
        };
    }
}
