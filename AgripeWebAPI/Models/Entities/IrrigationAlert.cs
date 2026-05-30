namespace AgripeWebAPI.Models.Entities
{
    public class IrrigationAlert : Entity
    {
        public int PivotId { get; set; }
        public int UserId { get; set; }
        public string AlertType { get; set; } = "humidity_low_projected";
        public decimal CurrentAverage { get; set; }
        public decimal ProjectedValue { get; set; }
        public decimal LimiteInferior { get; set; }
        public double SlopePerHour { get; set; }
        public DateTime Date { get; set; }
    }
}
