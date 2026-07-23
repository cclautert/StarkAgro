using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Services.Climate
{
    /// <summary>
    /// Centroide de uma área para a busca de previsão pontual. Puro e estático (disciplina de
    /// <c>MonitoredAreaGeometry</c>/<c>FireAreaBbox</c>) — o critério de aceite pede um teste sem
    /// mock cobrindo círculo e polígono.
    /// </summary>
    public static class AreaCentroid
    {
        /// <summary>
        /// Prefere <c>CenterLat/CenterLng</c> (round-trip do círculo); no polígono, cai para o
        /// centro do bbox da geometria. <c>null</c> quando não há como localizar a área (sem centro
        /// e sem geometria) — o worker pula em vez de estourar.
        /// </summary>
        public static (double lat, double lng)? Of(MonitoredArea area)
        {
            if (area.CenterLat is double lat && area.CenterLng is double lng)
                return (lat, lng);

            if (area.Geometry is not null)
            {
                var bbox = CdseProcessService.ComputeBbox(area.Geometry);
                return ((bbox.MinLat + bbox.MaxLat) / 2.0, (bbox.MinLng + bbox.MaxLng) / 2.0);
            }

            return null;
        }
    }
}
