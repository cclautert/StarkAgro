using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class DoseEvalscriptTests
    {
        private static FertilizationProfile Profile() => new()
        {
            Id = 1, Culture = "Café",
            Doses =
            [
                new ZoneDose { ClassKey = NdviClassification.Classes[^1].Key, NitrogenKgHa = 90, PhosphorusKgHa = 40, PotassiumKgHa = 60 },
                new ZoneDose { ClassKey = NdviClassification.Classes[3].Key, NitrogenKgHa = 12.5, PhosphorusKgHa = 20, PotassiumKgHa = 30 }
            ]
        };

        [Fact]
        public void Build_SaidaFloat32TresBandas()
        {
            var script = DoseEvalscript.Build(Profile());

            Assert.Contains("sampleType: \"FLOAT32\"", script);
            Assert.Contains("bands: 3", script);
            Assert.Contains("function dose(ndvi)", script);
            Assert.Contains("function evaluatePixel(s)", script);
        }

        [Fact]
        public void Build_ClasseComDose_EmiteODoseDoPerfil()
        {
            var script = DoseEvalscript.Build(Profile());

            // Classe High tem dose 90/40/60 → literal [90, 40, 60].
            Assert.Contains("[90, 40, 60]", script);
        }

        [Fact]
        public void Build_ClasseSemDose_EmiteZeros()
        {
            // Só High e Medium têm dose no perfil; as outras 4 classes saem [0, 0, 0].
            var script = DoseEvalscript.Build(Profile());
            Assert.Contains("[0, 0, 0]", script);
        }

        [Fact]
        public void Build_NuvemENoData_RetornaZeros()
        {
            var script = DoseEvalscript.Build(Profile());
            Assert.Contains("s.SCL === 3", script);           // máscara de nuvem preservada
            Assert.Contains("if (s.dataMask === 0 || cloud) return [0, 0, 0];", script);
        }

        [Fact]
        public void Build_UmIfPorClasseMenosAUltima()
        {
            var script = DoseEvalscript.Build(Profile());
            Assert.Equal(NdviClassification.Classes.Count - 1, Count(script, "if (ndvi <"));
        }

        [Fact]
        public void Build_PontoDecimalInvariante()
        {
            // Servidor em pt-BR formataria 12,5 e geraria JS inválido na CDSE.
            var script = DoseEvalscript.Build(Profile());
            Assert.Contains("12.5", script);
            Assert.DoesNotContain("12,5", script);
        }

        [Theory]
        [InlineData("N", "[90]")]
        [InlineData("P", "[40]")]
        [InlineData("K", "[60]")]
        public void Build_NutrienteUnico_UmaBandaComAquelaDose(string nutrient, string expected)
        {
            var script = DoseEvalscript.Build(Profile(), nutrient);

            Assert.Contains("bands: 1", script);
            Assert.Contains(expected, script);                       // High naquele nutriente
            Assert.Contains("return [0];", script);                  // classe sem dose / nuvem
        }

        [Theory]
        [InlineData("n", "N")]
        [InlineData("  k ", "K")]
        [InlineData("P", "P")]
        [InlineData("x", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Normalize_SoAceitaNPK(string? input, string? expected)
        {
            Assert.Equal(expected, DoseEvalscript.Normalize(input));
        }

        private static int Count(string haystack, string needle)
        {
            var n = 0;
            for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) n++;
            return n;
        }
    }
}
