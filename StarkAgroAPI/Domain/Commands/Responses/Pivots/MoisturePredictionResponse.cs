namespace StarkAgroAPI.Domain.Commands.Responses.Pivots
{
    public class MoisturePredictionResponse
    {
        public int PivotId { get; set; }
        public List<PredictedMoisturePoint> PredictedValues { get; set; } = new();
        public DateTime? EstimatedCriticalAt { get; set; }
        public double Confidence { get; set; }
        public int DataPointsUsed { get; set; }
    }

    public class PredictedMoisturePoint
    {
        public DateTime Date { get; set; }
        public double PredictedMoisture { get; set; }
        public double ConfidenceMin { get; set; }
        public double ConfidenceMax { get; set; }
    }
}
