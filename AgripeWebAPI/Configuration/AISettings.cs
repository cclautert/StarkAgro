namespace AgripeWebAPI.Configuration
{
    public class AISettings
    {
        public const string SectionName = "AI";

        public string AnthropicApiKey { get; set; } = "CHANGE_ME";
        public string Model { get; set; } = "claude-sonnet-4-6";
        public int MaxTokens { get; set; } = 1024;
        public int CacheDurationMinutes { get; set; } = 30;
    }
}
