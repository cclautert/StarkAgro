namespace StarkAgroAPI.Domain.Commands.Responses.Agronomist
{
    /// <summary>Uma linha do painel de faturamento: quanto um cliente consumiu e deve no mês.</summary>
    public class AgronomistBillingLine
    {
        public int ClientUserId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientEmail { get; set; }
        public string PlanName { get; set; } = "Sem plano";
        public int MonthlyPriceCents { get; set; }
        public int IncludedReports { get; set; }
        public int UsedReports { get; set; }
        public int OverageReports { get; set; }
        public int OveragePriceCents { get; set; }
        public int TotalCents { get; set; }
    }

    public class AgronomistBillingResponse
    {
        public List<AgronomistBillingLine> Clients { get; set; } = [];

        /// <summary>Soma do faturamento de todos os clientes ativos no mês, em centavos.</summary>
        public int TotalCents { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }
}
