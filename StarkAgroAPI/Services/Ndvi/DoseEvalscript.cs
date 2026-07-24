using System.Globalization;
using System.Text;
using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Gera o evalscript v3 que produz o <b>GeoTIFF de doses</b> (prescrição para taxa variável): o
    /// valor do pixel é a dose em kg/ha, não uma cor. A dose é uma <b>função-degrau do NDVI</b> pelos
    /// <b>mesmos cortes</b> de <see cref="NdviClassification.Classes"/> que colorem o mapa e alimentam
    /// o relatório — a mesma fonte da verdade, então os três artefatos nunca divergem. Puro e estático
    /// (disciplina de <see cref="NdviClassification"/>): sem I/O, testável offline.
    /// <para>
    /// Saída <c>FLOAT32</c> (pixel = número real). Fora do talhão / nuvem → 0 (0 kg/ha = não aplicar).
    /// Classe sem <see cref="ZoneDose"/> no perfil → 0. Com <paramref name="nutrient"/> "N"/"P"/"K",
    /// sai 1 banda daquele nutriente; sem ele, 3 bandas [N, P, K].
    /// </para>
    /// </summary>
    public static class DoseEvalscript
    {
        public static string Build(FertilizationProfile profile, string? nutrient = null)
        {
            var single = Normalize(nutrient);
            var bands = single is null ? 3 : 1;

            var sb = new StringBuilder();
            sb.AppendLine("//VERSION=3");
            sb.AppendLine("function setup() {");
            sb.AppendLine("  return {");
            sb.AppendLine("    input: [{ bands: [\"B04\", \"B08\", \"SCL\", \"dataMask\"] }],");
            sb.AppendLine($"    output: {{ bands: {bands}, sampleType: \"FLOAT32\" }}");
            sb.AppendLine("  };");
            sb.AppendLine("}");
            sb.AppendLine(BuildDoseFunction(profile, single));
            sb.AppendLine("function evaluatePixel(s) {");
            sb.AppendLine("  let cloud = (s.SCL === 3 || s.SCL === 8 || s.SCL === 9 || s.SCL === 10);");
            sb.AppendLine($"  if (s.dataMask === 0 || cloud) return {Zeros(bands)};");
            sb.AppendLine("  let ndvi = (s.B08 - s.B04) / (s.B08 + s.B04);");
            sb.AppendLine("  return dose(ndvi);");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Corpo JS da <c>dose(ndvi)</c>: um <c>if (ndvi &lt; corte)</c> por classe (menos a última,
        /// que vira o <c>return</c> final), retornando a dose da <see cref="ZoneDose"/> daquela classe.
        /// </summary>
        private static string BuildDoseFunction(FertilizationProfile profile, string? single)
        {
            var classes = NdviClassification.Classes;
            var sb = new StringBuilder();
            sb.AppendLine("function dose(ndvi) {");
            for (var i = 0; i < classes.Count - 1; i++)
            {
                sb.AppendLine(
                    $"  if (ndvi < {F(classes[i].HighEdge)}) return {DoseFor(profile, classes[i].Key, single)}; // {classes[i].Label}");
            }
            var last = classes[^1];
            sb.AppendLine($"  return {DoseFor(profile, last.Key, single)}; // {last.Label}");
            sb.Append('}');
            return sb.ToString();
        }

        // Dose de uma classe como literal JS: "[N, P, K]" ou "[valor]" (nutriente único). Classe sem
        // dose no perfil → zeros.
        private static string DoseFor(FertilizationProfile profile, string classKey, string? single)
        {
            var d = profile.Doses.FirstOrDefault(x => x.ClassKey == classKey);
            if (single is not null)
            {
                var v = single switch
                {
                    "N" => d?.NitrogenKgHa ?? 0,
                    "P" => d?.PhosphorusKgHa ?? 0,
                    _ => d?.PotassiumKgHa ?? 0
                };
                return $"[{F(v)}]";
            }
            return $"[{F(d?.NitrogenKgHa ?? 0)}, {F(d?.PhosphorusKgHa ?? 0)}, {F(d?.PotassiumKgHa ?? 0)}]";
        }

        /// <summary>"N"/"P"/"K" normalizado; <c>null</c> (3 bandas) para qualquer outra coisa.</summary>
        public static string? Normalize(string? nutrient)
        {
            var n = nutrient?.Trim().ToUpperInvariant();
            return n is "N" or "P" or "K" ? n : null;
        }

        private static string Zeros(int bands) => bands == 1 ? "[0]" : "[0, 0, 0]";

        // Ponto decimal sempre '.', independente da cultura do servidor — é código JS (a lição do ramp).
        private static string F(double v) => Math.Round(v, 3).ToString(CultureInfo.InvariantCulture);
    }
}
