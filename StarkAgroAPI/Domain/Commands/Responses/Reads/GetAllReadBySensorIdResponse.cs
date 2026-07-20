namespace StarkAgroAPI.Domain.Commands.Responses.Reads
{
    public class GetAllReadBySensorIdResponse
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public Decimal Value { get; set; }
        public DateTime Date { get; set; }
        public decimal? Humidity { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? BatteryVoltage { get; set; }
    }
}
