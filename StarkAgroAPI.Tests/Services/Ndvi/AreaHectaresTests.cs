using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class AreaHectaresTests
    {
        // Quadrado de 0,01° × 0,01° em torno de lat -23. Referência planar (área geodésica de célula
        // minúscula ≈ R²·Δφ·Δλ·cos φ) para conferir a fórmula esférica dentro de tolerância.
        [Fact]
        public void OfRing_QuadradoPequeno_BateComReferenciaPlanar()
        {
            const double lat = -23.0, lng = -47.0, d = 0.01;
            var ring = new List<GeoCoordinate>
            {
                new() { Lat = lat, Lng = lng },
                new() { Lat = lat, Lng = lng + d },
                new() { Lat = lat + d, Lng = lng + d },
                new() { Lat = lat + d, Lng = lng },
                new() { Lat = lat, Lng = lng } // fechado
            };

            const double r = 6_371_008.8;
            var rad = System.Math.PI / 180.0;
            var expectedHa = r * r * (d * rad) * (d * rad) * System.Math.Cos(lat * rad) / 10_000.0;

            var ha = AreaHectares.OfRing(ring);

            Assert.True(System.Math.Abs(ha - expectedHa) / expectedHa < 0.01,
                $"esperado ~{expectedHa:F2} ha, veio {ha:F2} ha");
            Assert.True(ha > 100 && ha < 130); // ~114 ha, sanidade
        }

        [Fact]
        public void OfRing_AnelAbertoOuFechado_MesmaArea()
        {
            var open = new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 },
                new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -47.0 }
            };
            var closed = new List<GeoCoordinate>(open) { new() { Lat = -23.0, Lng = -47.0 } };

            Assert.Equal(AreaHectares.OfRing(open), AreaHectares.OfRing(closed), 6);
        }

        [Fact]
        public void OfRing_OrdemDosVertices_NaoMudaAArea()
        {
            var cw = new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 },
                new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 }
            };
            var ccw = new List<GeoCoordinate>(cw);
            ccw.Reverse(); // sentido oposto → mesmo módulo de área

            Assert.Equal(AreaHectares.OfRing(cw), AreaHectares.OfRing(ccw), 6);
        }

        [Theory]
        [InlineData(0)] // vazio
        [InlineData(1)] // 1 ponto
        [InlineData(2)] // 2 pontos (degenerado)
        public void OfRing_MenosDeTresVertices_Zero(int count)
        {
            var ring = new List<GeoCoordinate>();
            for (var i = 0; i < count; i++) ring.Add(new GeoCoordinate { Lat = -23.0 + i * 0.01, Lng = -47.0 });

            Assert.Equal(0, AreaHectares.OfRing(ring));
        }

        [Fact]
        public void OfRing_Null_Zero()
        {
            Assert.Equal(0, AreaHectares.OfRing(null));
        }
    }
}
