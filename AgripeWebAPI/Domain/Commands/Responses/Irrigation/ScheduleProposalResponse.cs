namespace AgripeWebAPI.Domain.Commands.Responses.Irrigation
{
    public class ScheduleProposalResponse
    {
        public int ProposalId { get; set; }
        public List<IrrigationWindowDto> Windows { get; set; } = new();
    }
}
