namespace AgripeWebAPI.Models.Entities
{
    public class SensorAnomaly : Entity
    {
        public int SensorId { get; set; }
        public int UserId { get; set; }
        public int ReadSensorId { get; set; }
        public decimal Value { get; set; }
        public decimal ExpectedMin { get; set; }
        public decimal ExpectedMax { get; set; }
        public DateTime Date { get; set; }
        public bool Acknowledged { get; set; }
    }
}
