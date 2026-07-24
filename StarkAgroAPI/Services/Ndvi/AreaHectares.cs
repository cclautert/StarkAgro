using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// Área de um talhão em <b>hectares</b>, a partir do anel autoritativo da geometria. Puro e
    /// estático (disciplina de <see cref="AreaCentroid"/>/<see cref="MonitoredAreaGeometry"/>):
    /// testável sem mock, sem I/O. O círculo já chega como anel (o front aproxima antes de enviar),
    /// então não há caso especial — sempre o polígono.
    /// </summary>
    public static class AreaHectares
    {
        // Raio médio da Terra (IUGG), em metros. m² → ha divide por 10.000.
        private const double EarthRadiusMeters = 6_371_008.8;

        /// <summary>
        /// Hectares da área; <c>0</c> quando não há geometria/anel válido (o chamador trata como
        /// "sem área para prescrever" em vez de estourar).
        /// </summary>
        public static double Of(MonitoredArea area) => OfRing(MonitoredAreaGeometry.ToRing(area?.Geometry));

        /// <summary>
        /// Área geodésica de um anel lat/lng (fórmula padrão de polígono esférico). Precisa de ≥3
        /// vértices distintos; o anel pode vir fechado (último == primeiro) ou não — o módulo do
        /// somatório e o fechamento por <c>(i+1) % n</c> cuidam dos dois casos.
        /// </summary>
        public static double OfRing(IReadOnlyList<GeoCoordinate>? ring)
        {
            if (ring is null) return 0;

            // Remove o vértice de fechamento duplicado, se houver, para não contar uma aresta nula.
            var pts = ring.ToList();
            if (pts.Count > 1
                && Math.Abs(pts[0].Lat - pts[^1].Lat) < 1e-12
                && Math.Abs(pts[0].Lng - pts[^1].Lng) < 1e-12)
            {
                pts.RemoveAt(pts.Count - 1);
            }
            var n = pts.Count;
            if (n < 3) return 0;

            // Σ (λ_{i+1} − λ_i)·(2 + sinφ_i + sinφ_{i+1}), tudo em radianos.
            double sum = 0;
            for (var i = 0; i < n; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                var lam1 = Deg2Rad(a.Lng);
                var lam2 = Deg2Rad(b.Lng);
                var phi1 = Deg2Rad(a.Lat);
                var phi2 = Deg2Rad(b.Lat);
                sum += (lam2 - lam1) * (2 + Math.Sin(phi1) + Math.Sin(phi2));
            }

            var areaM2 = Math.Abs(sum) * EarthRadiusMeters * EarthRadiusMeters / 2.0;
            return areaM2 / 10_000.0;
        }

        private static double Deg2Rad(double deg) => deg * Math.PI / 180.0;
    }
}
