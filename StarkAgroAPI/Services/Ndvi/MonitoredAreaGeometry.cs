using StarkAgroAPI.Models;
using MongoDB.Driver.GeoJsonObjectModel;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Fábrica/validação pura da geometria de uma área. Isolada do Mongo e do MediatR para ser
    /// testável — o ponto crítico é a ordem <c>[lng, lat]</c> do GeoJSON, fonte clássica de bug.
    /// </summary>
    public static class MonitoredAreaGeometry
    {
        public const int MaxVertices = 500;

        /// <summary>Vão máximo do bounding box (~55 km). Bloqueia polígono gigante (custo de PU).</summary>
        public const double MaxSpanDegrees = 0.5;

        public static bool TryBuild(
            IReadOnlyList<GeoCoordinate>? ring,
            out GeoJsonPolygon<GeoJson2DGeographicCoordinates> polygon,
            out string? error)
        {
            polygon = default!;
            error = null;

            if (ring is null || ring.Count < 3)
            {
                error = "A área precisa de ao menos 3 pontos.";
                return false;
            }

            foreach (var p in ring)
            {
                if (p.Lat < -90 || p.Lat > 90 || p.Lng < -180 || p.Lng > 180)
                {
                    error = "Coordenada fora dos limites (lat -90..90, lng -180..180).";
                    return false;
                }
            }

            // Fecha o anel se o cliente não fechou.
            var pts = new List<GeoCoordinate>(ring);
            var first = pts[0];
            var last = pts[^1];
            if (Math.Abs(first.Lat - last.Lat) > 1e-9 || Math.Abs(first.Lng - last.Lng) > 1e-9)
            {
                pts.Add(new GeoCoordinate { Lat = first.Lat, Lng = first.Lng });
            }

            if (pts.Count > MaxVertices)
            {
                error = $"Polígono com vértices demais (máximo {MaxVertices}).";
                return false;
            }

            var distinct = pts.Take(pts.Count - 1)
                .Select(p => (Math.Round(p.Lat, 7), Math.Round(p.Lng, 7)))
                .Distinct()
                .Count();
            if (distinct < 3)
            {
                error = "A área precisa de ao menos 3 pontos distintos.";
                return false;
            }

            var minLat = pts.Min(p => p.Lat);
            var maxLat = pts.Max(p => p.Lat);
            var minLng = pts.Min(p => p.Lng);
            var maxLng = pts.Max(p => p.Lng);
            if ((maxLat - minLat) > MaxSpanDegrees || (maxLng - minLng) > MaxSpanDegrees)
            {
                error = "Área grande demais para monitorar. Reduza o polígono.";
                return false;
            }

            if (HasSelfIntersection(pts))
            {
                error = "O polígono tem arestas que se cruzam.";
                return false;
            }

            // Construção na ordem GeoJSON: (longitude, latitude).
            var coords = pts.Select(p => new GeoJson2DGeographicCoordinates(p.Lng, p.Lat)).ToList();
            polygon = new GeoJsonPolygon<GeoJson2DGeographicCoordinates>(
                new GeoJsonPolygonCoordinates<GeoJson2DGeographicCoordinates>(
                    new GeoJsonLinearRingCoordinates<GeoJson2DGeographicCoordinates>(coords)));
            return true;
        }

        /// <summary>Reconstrói o anel (lat/lng) a partir do polígono guardado.</summary>
        public static List<GeoCoordinate> ToRing(GeoJsonPolygon<GeoJson2DGeographicCoordinates>? polygon)
        {
            if (polygon?.Coordinates?.Exterior?.Positions is not { } positions) return [];
            return positions
                .Select(c => new GeoCoordinate { Lat = c.Latitude, Lng = c.Longitude })
                .ToList();
        }

        /// <summary>
        /// Checagem básica de auto-interseção: alguma aresta não-adjacente cruza outra? Não é
        /// validação topológica completa, mas pega o caso comum do "laço" no desenho.
        /// </summary>
        private static bool HasSelfIntersection(List<GeoCoordinate> ring)
        {
            var n = ring.Count - 1; // último ponto == primeiro (anel fechado)
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    // Pula arestas adjacentes (compartilham vértice) e o par que fecha o anel.
                    if (j == i || j == i + 1 || (i == 0 && j == n - 1)) continue;
                    if (SegmentsIntersect(ring[i], ring[i + 1], ring[j], ring[j + 1]))
                        return true;
                }
            }
            return false;
        }

        private static bool SegmentsIntersect(GeoCoordinate p1, GeoCoordinate p2, GeoCoordinate p3, GeoCoordinate p4)
        {
            double D(GeoCoordinate a, GeoCoordinate b, GeoCoordinate c) =>
                (b.Lng - a.Lng) * (c.Lat - a.Lat) - (b.Lat - a.Lat) * (c.Lng - a.Lng);

            var d1 = D(p3, p4, p1);
            var d2 = D(p3, p4, p2);
            var d3 = D(p1, p2, p3);
            var d4 = D(p1, p2, p4);

            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
                && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }
    }
}
