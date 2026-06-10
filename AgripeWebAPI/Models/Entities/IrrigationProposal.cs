namespace AgripeWebAPI.Models.Entities
{
    public enum ProposalStatus { Pending, Accepted, Rejected }

    public class IrrigationWindow
    {
        public int PivotId { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public double EstimatedMm { get; set; }
    }

    public class IrrigationProposal : Entity
    {
        public int UserId { get; set; }
        public int WaterSourceId { get; set; }
        public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
        public List<IrrigationWindow> Windows { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DecidedAt { get; set; }
    }
}
