namespace AgripeWebAPI.Models.Entities
{
    public class PlatformAiSettings : Entity
    {
        public string? OpenAiKey { get; set; }
        public string? OpenAiModel { get; set; } = "gpt-4o";
        public string? AnthropicKey { get; set; }
        public string? AnthropicModel { get; set; } = "claude-sonnet-4-6";
        public string? GeminiKey { get; set; }
        public string? GeminiModel { get; set; } = "gemini-1.5-flash";
        public string ActiveProvider { get; set; } = "gemini";
    }
}
