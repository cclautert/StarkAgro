namespace AgripeWebAPI.Domain.Commands.Responses.Pivots
{
    public class PivotAIInsightsResponse
    {
        public string Insights { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public bool FromCache { get; set; }
    }
}
