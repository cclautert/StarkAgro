namespace StarkAgroAPI.Domain.Commands.Responses.Sensors
{
    public class SensorTelemetryResponse
    {
        public int Quadrante { get; set; }
        public string DeviceEui { get; set; } = string.Empty;
        public decimal? Humidity { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? BatteryVoltage { get; set; }
        public decimal? BatteryPercent { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
