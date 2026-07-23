using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
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

        /// <summary>Credenciais CDSE (Copernicus) e o kill-switch do NDVI.</summary>
        public string? CdseClientId { get; set; }
        public string? CdseClientSecret { get; set; }
        public bool Sentinel2Enabled { get; set; }
        public bool ExtraIndicesEnabled { get; set; }
        public string? FirmsMapKey { get; set; }
        public bool FireAlertsEnabled { get; set; }
        public int FireAlertRadiusKm { get; set; }
        public bool ClimateAlertsEnabled { get; set; }
        public int FrostAlertTempC { get; set; }
        public int HeatAlertTempC { get; set; }

        [Range(0, 100000, ErrorMessage = "NdviCostCents deve estar entre 0 e 100000.")]
        public int NdviCostCents { get; set; } = 1;

        /// <summary>Teto mensal de custo NDVI (PU), em centavos. 0 = ilimitado.</summary>
        [Range(0, 100000000, ErrorMessage = "NdviMonthlyBudgetCents deve estar entre 0 e 100000000.")]
        public int NdviMonthlyBudgetCents { get; set; } = 0;

        /// <summary>Teto de áreas monitoradas por usuário. 0 = ilimitado.</summary>
        [Range(0, 100000, ErrorMessage = "NdviMaxAreasPerUser deve estar entre 0 e 100000.")]
        public int NdviMaxAreasPerUser { get; set; } = 0;
    }
}
