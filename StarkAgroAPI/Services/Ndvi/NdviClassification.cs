using System.Globalization;
using System.Text;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <param name="Key">Chave estável, gravada no documento — nunca renomear sem migração.</param>
    /// <param name="LowEdge">Início da faixa (inclusivo).</param>
    /// <param name="HighEdge">Fim da faixa (exclusivo, salvo a última classe).</param>
    /// <param name="HexColor">Cor da classe — a mesma no PNG do overlay e na legenda da tela.</param>
    public record NdviClass(string Key, string Label, double LowEdge, double HighEdge, string HexColor);

    /// <summary>
    /// Níveis de biomassa do NDVI: <b>a única fonte da verdade</b> de cortes e cores. Alimenta
    /// ao mesmo tempo o histograma pedido à Statistical API, o <c>ramp()</c> do evalscript que
    /// colore o PNG (<see cref="CdseProcessService"/>) e a legenda que o front desenha. Mudar
    /// um corte aqui move os três juntos — mudar em um só faria o mapa mentir para a legenda.
    /// <para>
    /// Classe estática e pura (mesma disciplina de <c>MonitoredAreaGeometry</c>): testável
    /// offline, sem mock e sem I/O.
    /// </para>
    /// </summary>
    public static class NdviClassification
    {
        /// <summary>Da menor para a maior biomassa. A ordem define a ordem dos bins do histograma.</summary>
        public static readonly IReadOnlyList<NdviClass> Classes =
        [
            new("BareSoil",   "Solo Exposto", -1.00, 0.20, "#d73027"),
            new("Low",        "Baixa",         0.20, 0.35, "#f46d43"),
            new("MediumLow",  "Média-Baixa",   0.35, 0.50, "#fdd992"),
            new("Medium",     "Média",         0.50, 0.65, "#e6f095"),
            new("MediumHigh", "Média-Alta",    0.65, 0.80, "#86cb66"),
            new("High",       "Alta",          0.80, 1.00, "#1a9641")
        ];

        // ── Histograma de largura fixa ──
        //
        // A Statistical API da CDSE **rejeita** (400 COMMON_BAD_PAYLOAD) um array explícito de
        // arestas em `histograms.bins`. Em vez de brigar com o formato, pedimos um histograma
        // fino e uniforme — `nBins`/`lowEdge`/`highEdge` são aceitos em toda versão da API — e
        // agregamos as classes aqui.
        //
        // Isso é melhor que o array por três razões: usa só campos universalmente suportados;
        // é imune a ruído de ponto flutuante nas bordas (a agregação é por ponto médio do bin,
        // nunca por igualdade); e desacopla a requisição da definição de classes — mudar um
        // corte depois é aritmética local, não um formato de request novo.

        // ⚠️ `decimal`, não `double`, e com o `.0` explícito — isto é serialização, não matemática.
        // A CDSE infere o TIPO do histograma pelo literal JSON: `-1` (inteiro) faz o servidor
        // responder 400 "sampleType AUTO mis-matched with corresponding histogram of type integer",
        // porque a banda NDVI é float. `System.Text.Json` escreve `double -1.0` como `-1` (forma
        // mais curta que faz round-trip), mas escreve `decimal -1.0m` como `-1.0`, preservando a
        // escala. Trocar estes dois para `double` reintroduz a falha — e ela só aparece em
        // produção, contra a API real.

        /// <summary>Aresta inferior do histograma pedido: o mínimo teórico do NDVI.</summary>
        public const decimal HistogramLowEdge = -1.0m;

        /// <summary>Aresta superior do histograma pedido: o máximo teórico do NDVI.</summary>
        public const decimal HistogramHighEdge = 1.0m;

        /// <summary>
        /// Número de bins. 200 sobre [-1, 1] dá largura 0,01 — fino o bastante para que todo
        /// corte de <see cref="Classes"/> caia numa fronteira de bin, sem inventar precisão.
        /// </summary>
        public const int HistogramBinCount = 200;

        /// <summary>
        /// Índice da classe que contém <paramref name="ndvi"/>, ou <c>-1</c> se estiver fora do
        /// domínio. Comparação <c>[LowEdge, HighEdge)</c>, com a última classe inclusiva no topo
        /// para que NDVI exatamente 1,0 não fique órfão.
        /// </summary>
        public static int ClassIndexFor(double ndvi)
        {
            for (var i = 0; i < Classes.Count; i++)
            {
                var c = Classes[i];
                if (ndvi >= c.LowEdge && (ndvi < c.HighEdge || (i == Classes.Count - 1 && ndvi <= c.HighEdge)))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Corpo JS da função <c>ramp()</c> do evalscript, derivado de <see cref="Classes"/>.
        /// Emite um <c>if</c> por classe (menos a última, que é o <c>return</c> final), com a
        /// cor em componentes 0-1 como o Sentinel Hub espera.
        /// </summary>
        public static string BuildRampFunction()
        {
            var sb = new StringBuilder();
            sb.AppendLine("function ramp(ndvi) {");
            for (var i = 0; i < Classes.Count - 1; i++)
            {
                var c = Classes[i];
                sb.AppendLine(
                    $"  if (ndvi < {F(c.HighEdge)}) return {ToJsRgb(c.HexColor)}; // {c.Label}");
            }
            var last = Classes[^1];
            sb.AppendLine($"  return {ToJsRgb(last.HexColor)}; // {last.Label}");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Percentual da área por classe. Denominador é a soma das contagens — passagem sem
        /// pixel válido (nublada) devolve tudo zero em vez de dividir por zero.
        /// </summary>
        public static double[] ToPercentages(IReadOnlyList<long> counts)
        {
            var result = new double[counts.Count];
            var total = counts.Sum();
            if (total <= 0) return result;

            for (var i = 0; i < counts.Count; i++)
                result[i] = 100.0 * counts[i] / total;
            return result;
        }

        /// <summary>Classe pela chave gravada; <c>null</c> para chave desconhecida (documento antigo).</summary>
        public static NdviClass? ByKey(string? key) =>
            key is null ? null : Classes.FirstOrDefault(c => c.Key == key);

        // "#d73027" → "[0.843, 0.188, 0.153]"
        private static string ToJsRgb(string hex)
        {
            var h = hex.TrimStart('#');
            var r = Convert.ToInt32(h[..2], 16) / 255.0;
            var g = Convert.ToInt32(h.Substring(2, 2), 16) / 255.0;
            var b = Convert.ToInt32(h.Substring(4, 2), 16) / 255.0;
            return $"[{F(r)}, {F(g)}, {F(b)}]";
        }

        // Ponto decimal sempre '.', independente da cultura do servidor — é código JS.
        private static string F(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);
    }
}
