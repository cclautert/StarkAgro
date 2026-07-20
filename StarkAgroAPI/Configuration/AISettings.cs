namespace StarkAgroAPI.Configuration
{
    public class AISettings
    {
        public const string SectionName = "AI";

        public string GeminiApiKey { get; set; } = "CHANGE_ME";
        public string Model { get; set; } = "gemini-1.5-flash";
        public int MaxTokens { get; set; } = 1024;
        public int CacheDurationMinutes { get; set; } = 30;
    }
}
