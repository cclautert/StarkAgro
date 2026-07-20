namespace StarkAgroAPI.Models
{
    /// <summary>
    /// Ponto geográfico no contrato REST — <b>nomeado</b> (lat/lng) para o cliente não precisar
    /// saber a ordem GeoJSON. A conversão para <c>[lng, lat]</c> é detalhe do servidor.
    /// </summary>
    public class GeoCoordinate
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
