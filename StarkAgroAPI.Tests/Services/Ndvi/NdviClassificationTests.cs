using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class NdviClassificationTests
    {
        [Fact]
        public void Classes_AreContiguousAndAscending()
        {
            var classes = NdviClassification.Classes;

            Assert.Equal(6, classes.Count);
            for (var i = 1; i < classes.Count; i++)
            {
                // Sem buraco nem sobreposição: o fim de uma classe é o começo da seguinte.
                // Um buraco deixaria pixel sem classe e a soma dos percentuais nunca fecharia 100.
                Assert.Equal(classes[i - 1].HighEdge, classes[i].LowEdge, 6);
                Assert.True(classes[i].LowEdge < classes[i].HighEdge);
            }
            Assert.Equal(-1.0, classes[0].LowEdge, 6);
            Assert.Equal(1.0, classes[^1].HighEdge, 6);
        }

        [Fact]
        public void Classes_HaveUniqueKeysAndLabels()
        {
            var classes = NdviClassification.Classes;

            Assert.Equal(classes.Count, classes.Select(c => c.Key).Distinct().Count());
            Assert.Equal(classes.Count, classes.Select(c => c.Label).Distinct().Count());
            Assert.All(classes, c => Assert.Matches("^#[0-9a-fA-F]{6}$", c.HexColor));
        }

        [Fact]
        public void HistogramBins_HasOneEdgeMoreThanClasses_AndIsSorted()
        {
            var bins = NdviClassification.HistogramBins;

            Assert.Equal(NdviClassification.Classes.Count + 1, bins.Length);
            Assert.Equal(bins.OrderBy(b => b), bins);
        }

        [Fact]
        public void ToPercentages_SumsToHundred()
        {
            var pct = NdviClassification.ToPercentages([10, 20, 30, 40, 0, 0]);

            Assert.Equal(100.0, pct.Sum(), 6);
            Assert.Equal(10.0, pct[0], 6);
            Assert.Equal(40.0, pct[3], 6);
        }

        [Fact]
        public void ToPercentages_AllZero_ReturnsZeroes_NotNaN()
        {
            // Passagem toda nublada: dividir pelo total zero daria NaN e envenenaria o gráfico.
            var pct = NdviClassification.ToPercentages([0, 0, 0, 0, 0, 0]);

            Assert.Equal(6, pct.Length);
            Assert.All(pct, p => Assert.Equal(0.0, p, 6));
        }

        [Fact]
        public void BuildRampFunction_EmitsOneCutPerClass_WithFinalFallback()
        {
            var ramp = NdviClassification.BuildRampFunction();

            Assert.StartsWith("function ramp(ndvi) {", ramp);
            Assert.EndsWith("}", ramp);
            // Um `if` por classe menos a última, que vira o return final.
            Assert.Equal(NdviClassification.Classes.Count - 1, Count(ramp, "if (ndvi <"));
            Assert.Equal(NdviClassification.Classes.Count, Count(ramp, "return ["));
        }

        [Fact]
        public void BuildRampFunction_UsesEveryClassColor_InZeroToOneComponents()
        {
            var ramp = NdviClassification.BuildRampFunction();

            // A cor de cada classe tem que aparecer no ramp: é isso que faz o PNG e a legenda
            // contarem a mesma história. Testa a primeira componente de cada hex.
            foreach (var c in NdviClassification.Classes)
            {
                var r = Math.Round(Convert.ToInt32(c.HexColor.Substring(1, 2), 16) / 255.0, 3)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                Assert.Contains($"[{r}, ", ramp);
            }
        }

        [Fact]
        public void BuildRampFunction_UsesInvariantDecimalPoint()
        {
            var ramp = NdviClassification.BuildRampFunction();

            // Servidor em pt-BR formataria 0,35 e geraria JS inválido na CDSE.
            Assert.DoesNotContain(",35", ramp);
            Assert.Contains("0.35", ramp);
        }

        [Theory]
        [InlineData("BareSoil", "Solo Exposto")]
        [InlineData("High", "Alta")]
        public void ByKey_KnownKey_ReturnsClass(string key, string label)
        {
            Assert.Equal(label, NdviClassification.ByKey(key)?.Label);
        }

        [Fact]
        public void ByKey_UnknownOrNull_ReturnsNull()
        {
            Assert.Null(NdviClassification.ByKey("Inexistente"));
            Assert.Null(NdviClassification.ByKey(null));
        }

        private static int Count(string haystack, string needle)
        {
            var n = 0;
            for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            {
                n++;
            }
            return n;
        }
    }
}
