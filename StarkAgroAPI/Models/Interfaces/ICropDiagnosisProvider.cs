using StarkAgroAPI.Services.CropHealth;

namespace StarkAgroAPI.Models.Interfaces
{
    /// <summary>
    /// Classificador especializado de doenças de planta a partir da foto.
    /// <para>
    /// É deliberadamente separado do LLM: modelos de linguagem genéricos acertam por volta de
    /// 62% dos diagnósticos de doença de planta em benchmark agrícola, enquanto classificadores
    /// dedicados passam de 90%. O laudo que um agrônomo assina não pode nascer de um chute —
    /// o LLM entra só para <i>redigir</i> o texto a partir deste resultado.
    /// </para>
    /// </summary>
    public interface ICropDiagnosisProvider
    {
        /// <summary>Devolve <c>null</c> quando o provedor falha (rede, chave inválida, resposta ilegível).</summary>
        Task<CropDiagnosisResult?> IdentifyAsync(
            CropDiagnosisInput input,
            string apiKey,
            CancellationToken cancellationToken);
    }
}
