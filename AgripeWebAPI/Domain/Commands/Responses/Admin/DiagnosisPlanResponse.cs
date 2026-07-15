namespace AgripeWebAPI.Domain.Commands.Responses.Admin
{
    public class DiagnosisPlanResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MonthlyPriceCents { get; set; }
        public int IncludedReportsPerMonth { get; set; }
        public int OveragePriceCents { get; set; }
        public bool Active { get; set; }
    }
}
