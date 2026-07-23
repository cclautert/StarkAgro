using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Climate;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Services.Climate
{
    public class AreaCentroidTests
    {
        [Fact]
        public void Of_Circulo_UsaCenterLatLng()
        {
            var area = new MonitoredArea { AreaKind = MonitoredAreaKind.Circle, CenterLat = -22.5, CenterLng = -47.3 };

            var c = AreaCentroid.Of(area);

            Assert.NotNull(c);
            Assert.Equal(-22.5, c!.Value.lat, 6);
            Assert.Equal(-47.3, c.Value.lng, 6);
        }

        [Fact]
        public void Of_Poligono_UsaOCentroDoBbox()
        {
            // Triângulo: bbox lat [-23,-22.99], lng [-47,-46.99] → centro (-22.995, -46.995).
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 }, new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);
            var area = new MonitoredArea { AreaKind = MonitoredAreaKind.Polygon, Geometry = geo };

            var c = AreaCentroid.Of(area);

            Assert.NotNull(c);
            Assert.Equal(-22.995, c!.Value.lat, 6);
            Assert.Equal(-46.995, c.Value.lng, 6);
        }

        [Fact]
        public void Of_CentroTemPrecedenciaSobreGeometria()
        {
            // Área com AMBOS: o centro explícito (round-trip do círculo) vence o bbox.
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -10.0, Lng = -50.0 }, new() { Lat = -10.0, Lng = -49.99 }, new() { Lat = -9.99, Lng = -49.99 }
            }, out var geo, out _);
            var area = new MonitoredArea { CenterLat = -22.5, CenterLng = -47.3, Geometry = geo };

            var c = AreaCentroid.Of(area);

            Assert.Equal(-22.5, c!.Value.lat, 6);
            Assert.Equal(-47.3, c.Value.lng, 6);
        }

        [Fact]
        public void Of_SemCentroESemGeometria_Null()
        {
            var area = new MonitoredArea { AreaKind = MonitoredAreaKind.Polygon, Geometry = null! };

            Assert.Null(AreaCentroid.Of(area));
        }

        [Fact]
        public void Of_SoUmaCoordenadaDeCentro_CaiParaGeometria()
        {
            // CenterLat sem CenterLng não é um centro válido — usa a geometria.
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 }, new() { Lat = -23.0, Lng = -46.99 }, new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);
            var area = new MonitoredArea { CenterLat = -22.5, CenterLng = null, Geometry = geo };

            var c = AreaCentroid.Of(area);

            Assert.Equal(-22.995, c!.Value.lat, 6); // do bbox, não do CenterLat órfão
        }
    }
}
