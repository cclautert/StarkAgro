namespace StarkAgroAPI.Models
{
    public record DailyForecast(DateOnly Date, double PrecipitationMm, double? ProbabilityPercent);

    public class WeatherForecast
    {
        public double TotalPrecipitationMm { get; init; }
        public IReadOnlyList<DailyForecast> DailyForecasts { get; init; } = Array.Empty<DailyForecast>();
        public string Source { get; init; } = string.Empty;
        public double? ProbabilityOfPrecipitation { get; init; }
        public bool IsAvailable { get; init; } = true;

        public static WeatherForecast Unavailable(string source) => new()
        {
            IsAvailable = false,
            Source = source,
            DailyForecasts = Array.Empty<DailyForecast>()
        };
    }
}
