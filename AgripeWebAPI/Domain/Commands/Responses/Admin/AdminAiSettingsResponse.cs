namespace AgripeWebAPI.Domain.Commands.Responses.Admin
{
    public class AdminAiSettingsResponse
    {
        public string? OpenAiKey { get; set; }
        public string? OpenAiModel { get; set; }
        public string? AnthropicKey { get; set; }
        public string? AnthropicModel { get; set; }
        public string? GeminiKey { get; set; }
        public string? GeminiModel { get; set; }
        public string ActiveProvider { get; set; } = "gemini";

        /// <summary>Classificador de doenças de planta (crop.health / Kindwise) — cobra por foto.</summary>
        public string? CropHealthKey { get; set; }
        public bool CropHealthEnabled { get; set; }
    }
}
