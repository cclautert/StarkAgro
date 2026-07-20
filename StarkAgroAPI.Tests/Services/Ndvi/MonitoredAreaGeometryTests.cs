using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class MonitoredAreaGeometryTests
    {
        private static List<GeoCoordinate> Square() => new()
        {
            new() { Lat = -23.00, Lng = -47.00 },
            new() { Lat = -23.00, Lng = -46.99 },
            new() { Lat = -22.99, Lng = -46.99 },
            new() { Lat = -22.99, Lng = -47.00 }
        };

        [Fact]
        public void TryBuild_ValidRing_StoresCoordinatesInLngLatOrder()
        {
            var ring = Square();

            var ok = MonitoredAreaGeometry.TryBuild(ring, out var polygon, out var error);

            Assert.True(ok);
            Assert.Null(error);
            var positions = polygon.Coordinates.Exterior.Positions;
            // Ordem GeoJSON: X = longitude, Y = latitude.
            Assert.Equal(ring[0].Lng, positions[0].Longitude, 9);
            Assert.Equal(ring[0].Lat, positions[0].Latitude, 9);
        }

        [Fact]
        public void TryBuild_OpenRing_IsAutoClosed()
        {
            var ok = MonitoredAreaGeometry.TryBuild(Square(), out var polygon, out _);

            Assert.True(ok);
            var positions = polygon.Coordinates.Exterior.Positions;
            Assert.Equal(5, positions.Count); // 4 + fechamento
            Assert.Equal(positions[0].Longitude, positions[^1].Longitude, 9);
            Assert.Equal(positions[0].Latitude, positions[^1].Latitude, 9);
        }

        [Fact]
        public void ToRing_RoundTripsBackToLatLng()
        {
            MonitoredAreaGeometry.TryBuild(Square(), out var polygon, out _);

            var ring = MonitoredAreaGeometry.ToRing(polygon);

            Assert.Equal(-23.00, ring[0].Lat, 9);
            Assert.Equal(-47.00, ring[0].Lng, 9);
        }

        [Fact]
        public void TryBuild_FewerThanThreePoints_Fails()
        {
            var ring = new List<GeoCoordinate> { new() { Lat = 0, Lng = 0 }, new() { Lat = 0, Lng = 1 } };

            var ok = MonitoredAreaGeometry.TryBuild(ring, out _, out var error);

            Assert.False(ok);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryBuild_Null_Fails()
        {
            Assert.False(MonitoredAreaGeometry.TryBuild(null, out _, out _));
        }

        [Theory]
        [InlineData(91, 0)]
        [InlineData(-91, 0)]
        [InlineData(0, 181)]
        [InlineData(0, -181)]
        public void TryBuild_OutOfRangeCoordinate_Fails(double lat, double lng)
        {
            var ring = new List<GeoCoordinate>
            {
                new() { Lat = lat, Lng = lng },
                new() { Lat = 0, Lng = 0.01 },
                new() { Lat = 0.01, Lng = 0.01 }
            };

            Assert.False(MonitoredAreaGeometry.TryBuild(ring, out _, out var error));
            Assert.NotNull(error);
        }

        [Fact]
        public void TryBuild_FewerThanThreeDistinctPoints_Fails()
        {
            // 3 pontos, mas dois idênticos → menos de 3 vértices distintos.
            var ring = new List<GeoCoordinate>
            {
                new() { Lat = 0, Lng = 0 },
                new() { Lat = 0, Lng = 0 },
                new() { Lat = 0.01, Lng = 0.01 }
            };

            Assert.False(MonitoredAreaGeometry.TryBuild(ring, out _, out var error));
            Assert.Contains("distintos", error);
        }

        [Fact]
        public void TryBuild_TooManyVertices_Fails()
        {
            // Círculo aproximado com vértices demais (> MaxVertices), num bbox pequeno.
            var ring = new List<GeoCoordinate>();
            for (var i = 0; i < 501; i++)
            {
                var ang = 2 * Math.PI * i / 501;
                ring.Add(new GeoCoordinate { Lat = 0.001 * Math.Sin(ang), Lng = 0.001 * Math.Cos(ang) });
            }

            Assert.False(MonitoredAreaGeometry.TryBuild(ring, out _, out var error));
            Assert.Contains("vértices", error);
        }

        [Fact]
        public void TryBuild_GiantPolygon_Fails()
        {
            var ring = new List<GeoCoordinate>
            {
                new() { Lat = 0, Lng = 0 },
                new() { Lat = 0, Lng = 10 },   // 10° > MaxSpanDegrees (0.5)
                new() { Lat = 10, Lng = 10 },
                new() { Lat = 10, Lng = 0 }
            };

            Assert.False(MonitoredAreaGeometry.TryBuild(ring, out _, out var error));
            Assert.Contains("grande", error);
        }

        [Fact]
        public void TryBuild_SelfIntersecting_Fails()
        {
            // Bowtie pequeno (dentro de 0.5°): as diagonais se cruzam.
            var ring = new List<GeoCoordinate>
            {
                new() { Lat = 0.00, Lng = 0.00 },
                new() { Lat = 0.10, Lng = 0.10 },
                new() { Lat = 0.00, Lng = 0.10 },
                new() { Lat = 0.10, Lng = 0.00 }
            };

            Assert.False(MonitoredAreaGeometry.TryBuild(ring, out _, out var error));
            Assert.Contains("cruzam", error);
        }
    }
}
