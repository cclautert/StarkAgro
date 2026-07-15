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

        /// <summary>Chave do crop.health (Kindwise) e o kill-switch do laudo fitossanitário.</summary>
        public string? CropHealthKey { get; set; }
        public bool CropHealthEnabled { get; set; }

        /// <summary>Cota padrão de laudos/mês. 0 = ilimitado.</summary>
        public int DefaultDiagnosisQuotaPerMonth { get; set; }

        /// <summary>Custo por análise do classificador, em centavos (evita float em dinheiro).</summary>
        [Range(0, 100000, ErrorMessage = "CropHealthCostCents deve estar entre 0 e 100000.")]
        public int CropHealthCostCents { get; set; } = 3;

        [Required]
        [RegularExpression("^(openai|anthropic|gemini)$", ErrorMessage = "ActiveProvider deve ser 'openai', 'anthropic' ou 'gemini'.")]
        public string ActiveProvider { get; set; } = "gemini";
    }
}
