namespace StarkAgroAPI.Services.Platform
{
    /// <summary>
    /// Lista canônica de culturas mais comuns do Brasil (a plataforma já nasce com elas) e a lógica
    /// pura de merge/dedup usada no seed do boot. Estático e sem I/O (disciplina de
    /// <c>NdviClassification</c>): o boot decide o que persistir a partir daqui, testável offline.
    /// </summary>
    public static class CultureSeed
    {
        /// <summary>Culturas pré-cadastradas (o admin depois só ajusta em /admin/culturas).</summary>
        public static readonly IReadOnlyList<string> Canonical =
        [
            "Soja", "Milho", "Milho 2ª safra (safrinha)", "Café", "Algodão", "Cana-de-açúcar",
            "Trigo", "Feijão", "Arroz", "Sorgo", "Girassol", "Amendoim", "Aveia", "Cevada",
            "Canola", "Milheto", "Mandioca", "Pastagem", "Citros/Laranja", "Eucalipto"
        ];

        /// <summary>
        /// Une a lista canônica com os valores já em uso (áreas/perfis/diagnósticos), <b>deduplicando
        /// por nome sem diferenciar caixa/acentuação-de-espaço</b> — "Café" e "café" viram um só. A
        /// forma preservada é a da 1ª ocorrência (canônica vence). Vazios/nulos são descartados.
        /// Resultado ordenado alfabeticamente (pt-BR, case-insensitive).
        /// </summary>
        public static List<string> Merge(IEnumerable<string> canonical, IEnumerable<string?> existing)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var name in canonical.Concat(existing.Select(e => e ?? string.Empty)))
            {
                var trimmed = name.Trim();
                if (trimmed.Length == 0) continue;
                if (seen.Add(trimmed)) result.Add(trimmed);
            }

            result.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }
}
