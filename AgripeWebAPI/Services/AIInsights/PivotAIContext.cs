namespace AgripeWebAPI.Services.AIInsights
{
    public class PivotAIContext
    {
        public string PivotName { get; set; } = string.Empty;
        public decimal LimiteInferior { get; set; }
        public decimal LimiteSuperior { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public List<SensorReadingContext> SensorReadings { get; set; } = [];
        public string? ForecastSummary { get; set; }
        public List<AnomalyContext> RecentAnomalies { get; set; } = [];
        public string? ApiKeyOverride { get; set; }
    }

    public class SensorReadingContext
    {
        public string? SensorCode { get; set; }
        public int Quadrante { get; set; }
        public List<ReadingPoint> Readings { get; set; } = [];
    }

    public class ReadingPoint
    {
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
    }

    public class AnomalyContext
    {
        public int SensorId { get; set; }
        public decimal Value { get; set; }
        public decimal ExpectedMin { get; set; }
        public decimal ExpectedMax { get; set; }
        public DateTime Date { get; set; }
    }
}
