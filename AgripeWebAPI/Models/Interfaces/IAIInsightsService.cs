using AgripeWebAPI.Services.AIInsights;

namespace AgripeWebAPI.Models.Interfaces
{
    public interface IAIInsightsService
    {
        Task<string?> GetInsightsAsync(PivotAIContext context, CancellationToken cancellationToken);
    }
}
