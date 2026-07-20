namespace StarkAgroAPI.Domain.Commands.Responses.Anomalies
{
    public class SensorAnomalyResponse
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public int PivotId { get; set; }
        public int ReadSensorId { get; set; }
        public decimal Value { get; set; }
        public decimal ExpectedMin { get; set; }
        public decimal ExpectedMax { get; set; }
        public DateTime Date { get; set; }
        public bool Acknowledged { get; set; }
    }
}
