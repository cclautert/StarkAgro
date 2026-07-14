using AgripeWebAPI.Services.Diagnosis;
using System.Globalization;

namespace AgripeWebAPI.Tests.Services
{
    /// <summary>
    /// Regressão pega pela CI, não pela máquina de dev: <c>{p:P0}</c> depende de cultura.
    /// No Windows sai "78%"; na cultura invariante — a do container Linux em produção — sai
    /// "78 %", com espaço antes do sinal. O laudo, o PDF e o prompt do LLM sairiam diferentes
    /// no dev e em produção.
    /// </summary>
    public class ProbabilityFormatterTests
    {
        [Theory]
        [InlineData(0.78, "78%")]
        [InlineData(0.42, "42%")]
        [InlineData(0.0, "0%")]
        [InlineData(1.0, "100%")]
        [InlineData(0.055, "6%")]
        public void ToPercent_FormatsWithoutSpace(double fraction, string expected)
        {
            Assert.Equal(expected, ProbabilityFormatter.ToPercent(fraction));
        }

        [Fact]
        public void ToPercent_IsTheSameInEveryCulture()
        {
            var original = CultureInfo.CurrentCulture;

            try
            {
                foreach (var culture in CulturesToCheck())
                {
                    CultureInfo.CurrentCulture = culture;

                    Assert.Equal("78%", ProbabilityFormatter.ToPercent(0.78));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        /// <summary>
        /// A cultura invariante é a que importa (é a do container em produção). As nomeadas são
        /// um bônus, e só entram quando existem — sob
        /// <c>DOTNET_SYSTEM_GLOBALIZATION_INVARIANT</c> o .NET recusa criá-las.
        /// </summary>
        private static IEnumerable<CultureInfo> CulturesToCheck()
        {
            yield return CultureInfo.InvariantCulture;

            foreach (var name in new[] { "pt-BR", "en-US", "de-DE" })
            {
                CultureInfo? culture = null;
                try { culture = new CultureInfo(name); }
                catch (CultureNotFoundException) { /* globalização invariante: pula */ }

                if (culture is not null) yield return culture;
            }
        }
    }
}
