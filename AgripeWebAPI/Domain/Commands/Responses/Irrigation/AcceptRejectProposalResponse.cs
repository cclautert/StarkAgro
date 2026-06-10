namespace AgripeWebAPI.Domain.Commands.Responses.Irrigation
{
    public class AcceptRejectProposalResponse
    {
        public int ProposalId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DecidedAt { get; set; }
    }
}
