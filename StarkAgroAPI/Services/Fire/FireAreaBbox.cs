using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Services.Fire
{
    /// <summary>
    /// Geometria pura da vigilância de fogo: expande o bbox da área pelo raio de alerta e mede a
    /// distância de um foco ao centro. Estática e sem I/O (mesma disciplina de
    /// <c>MonitoredAreaGeometry</c>) — testável offline, que é o que o parser do FIRMS não é sem
    /// uma MAP_KEY real.
    /// </summary>
    public static class FireAreaBbox
    {
        private const double KmPerDegreeLat = 111.0;

        /// <summary>
        /// <see cref="NdviBbox"/> expandido por <paramref name="radiusKm"/> em cada direção.
        /// A correção de longitude por <c>cos(lat)</c> usa o centro do bbox — a mesma conta que o
        /// <c>circleToRing</c> do front faz para não achatar o círculo longe do equador.
        /// </summary>
        public static NdviBbox Expand(NdviBbox bbox, double radiusKm)
        {
            var centerLat = (bbox.MinLat + bbox.MaxLat) / 2.0;
            var dLat = radiusKm / KmPerDegreeLat;
            // cos(lat) nunca chega a 0 em terra habitada, mas guardamos o piso para não estourar.
            var cosLat = Math.Max(0.01, Math.Cos(centerLat * Math.PI / 180.0));
            var dLng = radiusKm / (KmPerDegreeLat * cosLat);

            return new NdviBbox(
                bbox.MinLng - dLng, bbox.MinLat - dLat,
                bbox.MaxLng + dLng, bbox.MaxLat + dLat);
        }

        /// <summary>Distância aproximada (km) entre dois pontos — equirretangular, boa em escala de talhão.</summary>
        public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            var meanLat = (lat1 + lat2) / 2.0 * Math.PI / 180.0;
            var x = (lng2 - lng1) * Math.Cos(meanLat);
            var y = lat2 - lat1;
            return Math.Sqrt(x * x + y * y) * KmPerDegreeLat;
        }

        /// <summary>Centro do bbox (lat, lng), para medir a distância dos focos.</summary>
        public static (double lat, double lng) Center(NdviBbox bbox) =>
            ((bbox.MinLat + bbox.MaxLat) / 2.0, (bbox.MinLng + bbox.MaxLng) / 2.0);
    }
}
