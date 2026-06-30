using MediatR;
using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Domain.Commands.Requests.Admin
{
    public class UpdatePlatformAiSettingsRequest : IRequest<bool>
    {
        public string? OpenAiKey { get; set; }
        public string? OpenAiModel { get; set; }
        public string? AnthropicKey { get; set; }
        public string? AnthropicModel { get; set; }
        public string? GeminiKey { get; set; }
        public string? GeminiModel { get; set; }

        [Required]
        [RegularExpression("^(openai|anthropic|gemini)$", ErrorMessage = "ActiveProvider deve ser 'openai', 'anthropic' ou 'gemini'.")]
        public string ActiveProvider { get; set; } = "gemini";
    }
}
