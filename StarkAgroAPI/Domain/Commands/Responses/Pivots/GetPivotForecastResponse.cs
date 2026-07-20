using StarkAgroAPI.Models;

namespace StarkAgroAPI.Domain.Commands.Responses.Pivots
{
    public class GetPivotForecastResponse
    {
        public int PivotId { get; set; }
        public string? PivotName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int Days { get; set; }
        public bool HasCoordinates { get; set; }
        public WeatherForecast? Forecast { get; set; }
        public string? Message { get; set; }
    }
}
