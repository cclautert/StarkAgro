namespace StarkAgroAPI.Domain.Commands.Responses.Irrigation
{
    public class IrrigationWindowDto
    {
        public int PivotId { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public double EstimatedMm { get; set; }
    }
}
