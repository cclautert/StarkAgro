using StarkAgroAPI.Services.Fire;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Services.Fire
{
    public class FireAreaBboxTests
    {
        [Fact]
        public void Expand_AumentaOBboxNasQuatroDirecoes()
        {
            var box = new NdviBbox(-47.0, -22.0, -46.99, -21.99);

            var e = FireAreaBbox.Expand(box, 10);

            Assert.True(e.MinLng < box.MinLng);
            Assert.True(e.MinLat < box.MinLat);
            Assert.True(e.MaxLng > box.MaxLng);
            Assert.True(e.MaxLat > box.MaxLat);
        }

        [Fact]
        public void Expand_10km_DaCercaDe009GrausDeLatitude()
        {
            var box = new NdviBbox(-47.0, -22.0, -47.0, -22.0);

            var e = FireAreaBbox.Expand(box, 10);

            // 10 km / 111 km/grau ≈ 0,090 grau de latitude em cada lado.
            Assert.Equal(0.090, box.MinLat - e.MinLat, 3);
        }

        [Fact]
        public void Expand_LongitudeEsticaMaisQueLatitude_LongeDoEquador()
        {
            // Em -22°, cos(lat) ≈ 0,927 → o passo de longitude é maior que o de latitude.
            var box = new NdviBbox(-47.0, -22.0, -47.0, -22.0);

            var e = FireAreaBbox.Expand(box, 10);

            var dLat = box.MinLat - e.MinLat;
            var dLng = box.MinLng - e.MinLng;
            Assert.True(dLng > dLat);
        }

        [Fact]
        public void DistanceKm_MesmoPonto_Zero()
        {
            Assert.Equal(0, FireAreaBbox.DistanceKm(-22.0, -47.0, -22.0, -47.0), 3);
        }

        [Fact]
        public void DistanceKm_UmGrauDeLatitude_CercaDe111km()
        {
            var d = FireAreaBbox.DistanceKm(-22.0, -47.0, -23.0, -47.0);

            Assert.Equal(111.0, d, 0);
        }

        [Fact]
        public void Center_DevolveOMeioDoBbox()
        {
            var (lat, lng) = FireAreaBbox.Center(new NdviBbox(-48.0, -24.0, -46.0, -22.0));

            Assert.Equal(-23.0, lat, 6);
            Assert.Equal(-47.0, lng, 6);
        }
    }
}
