using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class FertilizationProfileResolverTests
    {
        private static MonitoredArea Area(string? crop) => new() { Id = 5, Crop = crop };
        private static FertilizationProfile P(int id, string culture) => new() { Id = id, Culture = culture };

        [Fact]
        public void Resolve_ProfileId_VenceOAutoMatch()
        {
            var profiles = new List<FertilizationProfile> { P(1, "Café"), P(7, "Milho") };
            var (profile, error) = FertilizationProfileResolver.Resolve(profiles, Area("Café"), profileId: 7);

            Assert.Null(error);
            Assert.Equal(7, profile!.Id);
        }

        [Fact]
        public void Resolve_ProfileIdInexistente_Erro()
        {
            var (profile, error) = FertilizationProfileResolver.Resolve([P(1, "Café")], Area("Café"), profileId: 999);

            Assert.Null(profile);
            Assert.NotNull(error);
        }

        [Theory]
        [InlineData("Café")]
        [InlineData("  café ")]
        [InlineData("CAFÉ")]
        public void Resolve_CulturaCasaIgnorandoCaseEEspaco(string crop)
        {
            var (profile, error) = FertilizationProfileResolver.Resolve([P(1, "Café")], Area(crop), profileId: null);

            Assert.Null(error);
            Assert.Equal(1, profile!.Id);
        }

        [Fact]
        public void Resolve_SemCultura_Erro()
        {
            var (profile, error) = FertilizationProfileResolver.Resolve([P(1, "Café")], Area(null), profileId: null);

            Assert.Null(profile);
            Assert.NotNull(error);
        }

        [Fact]
        public void Resolve_SemPerfilDaCultura_Erro()
        {
            var (profile, error) = FertilizationProfileResolver.Resolve([P(1, "Soja")], Area("Café"), profileId: null);

            Assert.Null(profile);
            Assert.Contains("Café", error);
        }
    }
}
