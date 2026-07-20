using StarkAgroAPI.Models;

namespace StarkAgroAPI.Domain.Commands.Responses.Pivots
{
    public class IrrigationTrendResponse
    {
        public int PivotId { get; set; }
        public string? PivotName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public decimal? LimiteInferior { get; set; }
        public decimal? LimiteSuperior { get; set; }
        public decimal? CurrentAverage { get; set; }
        public bool NeedsIrrigation { get; set; }
        public bool IrrigationPostponed { get; set; }
        public string? PostponeReason { get; set; }
        public WeatherForecast? WeatherForecast { get; set; }
    }
}
