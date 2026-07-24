using StarkAgroAPI.Services.Platform;

namespace StarkAgroAPI.Tests.Services.Platform
{
    public class CultureSeedTests
    {
        [Fact]
        public void Canonical_TemAsCulturasComunsDoBrasil()
        {
            Assert.Contains("Soja", CultureSeed.Canonical);
            Assert.Contains("Milho", CultureSeed.Canonical);
            Assert.Contains("Café", CultureSeed.Canonical);
            Assert.True(CultureSeed.Canonical.Count >= 15);
        }

        [Fact]
        public void Merge_DeduplicaIgnorandoCaseEEspaco()
        {
            // "café", " CAFÉ " e "Café" são a MESMA cultura — a 1ª forma (canônica) vence.
            var result = CultureSeed.Merge(["Café", "Soja"], [" café ", "CAFÉ", "soja"]);

            Assert.Equal(2, result.Count);
            Assert.Contains("Café", result);
            Assert.Contains("Soja", result);
        }

        [Fact]
        public void Merge_AdicionaExtrasNaoCanonicas()
        {
            var result = CultureSeed.Merge(["Soja"], ["Tomate", "Uva"]);

            Assert.Contains("Tomate", result);
            Assert.Contains("Uva", result);
            Assert.Contains("Soja", result);
        }

        [Fact]
        public void Merge_DescartaVaziosENulos()
        {
            var result = CultureSeed.Merge(["Soja"], [null, "", "   "]);
            Assert.Equal(["Soja"], result);
        }

        [Fact]
        public void Merge_OrdenaAlfabeticamente()
        {
            var result = CultureSeed.Merge(["Milho", "Arroz"], ["Trigo"]);
            Assert.Equal(["Arroz", "Milho", "Trigo"], result);
        }

        [Fact]
        public void Merge_Trima()
        {
            var result = CultureSeed.Merge([], ["  Sorgo  "]);
            Assert.Equal(["Sorgo"], result);
        }
    }
}
