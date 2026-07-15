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

        /// <summary>Chave do classificador de doenças (crop.health / Kindwise). Cobra por foto.</summary>
        public string? CropHealthKey { get; set; }

        /// <summary>
        /// Kill-switch do laudo fitossanitário. Desligado, nenhuma foto vira chamada paga —
        /// o admin corta o custo sem redeploy.
        /// </summary>
        public bool CropHealthEnabled { get; set; } = false;

        /// <summary>
        /// Cota padrão de laudos por mês, para produtores sem cota própria.
        /// <c>0</c> = ilimitado (o comportamento de antes desta configuração existir).
        /// </summary>
        public int DefaultDiagnosisQuotaPerMonth { get; set; } = 0;

        /// <summary>
        /// Custo de uma análise do classificador (crop.health / Kindwise), em <b>centavos</b>.
        /// <para>
        /// Guardado em centavos inteiros de propósito — dinheiro em <c>double</c> acumula erro, e
        /// o projeto já tropeçou em cultura/decimal. O Kindwise cobra ~€0,01–0,05 por foto; o
        /// padrão 3 (€0,03) fica no meio da faixa. O admin ajusta conforme o plano contratado.
        /// </para>
        /// </summary>
        public int CropHealthCostCents { get; set; } = 3;
    }
}
