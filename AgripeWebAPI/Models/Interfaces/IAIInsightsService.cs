using AgripeWebAPI.Services.AIInsights;

namespace AgripeWebAPI.Models.Interfaces
{
    public interface IAIInsightsService
    {
        Task<string?> GetInsightsAsync(PivotAIContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Canal genérico de texto para o mesmo provedor: recebe um system prompt e uma
        /// mensagem já montados e devolve a resposta. Existe para que o laudo fitossanitário
        /// reuse o cliente HTTP e as chaves dos insights, sem duplicar integração de LLM.
        /// Devolve <c>null</c> quando o provedor falha.
        /// </summary>
        /// <param name="maxTokens">
        /// Teto de tokens da resposta. Omitir usa o valor de <c>AISettings</c> (1024), que é
        /// suficiente para um insight curto mas <b>corta um laudo pelo meio</b> — inclusive o
        /// disclaimer do rodapé. O laudo pede um teto maior.
        /// </param>
        Task<string?> CompleteAsync(
            string systemPrompt,
            string userMessage,
            string? apiKey,
            string? model,
            CancellationToken cancellationToken,
            int? maxTokens = null);
    }
}
