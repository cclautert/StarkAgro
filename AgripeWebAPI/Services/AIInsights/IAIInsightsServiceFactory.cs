using AgripeWebAPI.Models.Interfaces;

namespace AgripeWebAPI.Services.AIInsights
{
    public interface IAIInsightsServiceFactory
    {
        IAIInsightsService? GetService(string provider);
    }

    public class AIInsightsServiceFactory : IAIInsightsServiceFactory
    {
        private readonly IServiceProvider _sp;

        public AIInsightsServiceFactory(IServiceProvider sp)
            => _sp = sp ?? throw new ArgumentNullException(nameof(sp));

        public IAIInsightsService? GetService(string provider) => provider.ToLower() switch
        {
            "gemini"    => _sp.GetRequiredService<GeminiInsightsService>(),
            "anthropic" => _sp.GetRequiredService<AnthropicInsightsService>(),
            _           => null
        };
    }
}
