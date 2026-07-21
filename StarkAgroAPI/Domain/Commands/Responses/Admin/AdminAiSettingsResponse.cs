namespace StarkAgroAPI.Domain.Commands.Responses.Admin
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
        public int DefaultDiagnosisQuotaPerMonth { get; set; }

        /// <summary>Custo configurado por análise do classificador, em centavos.</summary>
        public int CropHealthCostCents { get; set; }

        /// <summary>
        /// Custo de IA já incorrido no mês corrente, em centavos (só leitura). Torna o gasto
        /// visível onde o admin controla o botão de custo — sem tela nova.
        /// </summary>
        public int CurrentMonthAiCostCents { get; set; }

        // ── NDVI Sentinel-2 (CDSE) ──
        public string? CdseClientId { get; set; }
        public string? CdseClientSecret { get; set; }
        public bool Sentinel2Enabled { get; set; }
        public int NdviCostCents { get; set; }
    }
}
