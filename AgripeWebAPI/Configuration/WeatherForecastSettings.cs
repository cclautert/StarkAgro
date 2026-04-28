namespace AgripeWebAPI.Configuration
{
    public class WeatherForecastSettings
    {
        public const string SectionName = "WeatherForecast";

        public int ForecastHorizonDays { get; set; } = 5;
        public double RainThresholdMm { get; set; } = 5.0;
        public string PrimarySource { get; set; } = "OpenMeteo";
        public string FallbackSource { get; set; } = "OpenMeteo";
        public string GoogleWeatherApiKey { get; set; } = "CHANGE_ME";
        public int CacheDurationMinutes { get; set; } = 60;
        public int PivotDashboardForecastDays { get; set; } = 7;
    }
}
