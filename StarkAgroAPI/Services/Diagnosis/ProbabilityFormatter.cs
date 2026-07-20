using System.Globalization;

namespace StarkAgroAPI.Services.Diagnosis
{
    /// <summary>
    /// Formata a probabilidade do classificador como percentual.
    /// <para>
    /// Existe porque <c>{p:P0}</c> é <b>dependente de cultura</b>: no Windows sai "78%", mas na
    /// cultura invariante — que é a do container Linux onde a API roda em produção — sai
    /// "78 %", com um espaço antes do sinal. O laudo, o PDF e o prompt do LLM sairiam
    /// formatados de um jeito no dev e de outro em produção.
    /// </para>
    /// </summary>
    public static class ProbabilityFormatter
    {
        /// <summary>Recebe a fração (0..1) e devolve, por exemplo, <c>"78%"</c>.</summary>
        public static string ToPercent(double fraction)
            => Math.Round(fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
