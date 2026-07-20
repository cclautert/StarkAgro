using StarkAgroAPI.Domain.Commands.Responses.Irrigation;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Irrigation
{
    public class ScheduleProposalRequest : IRequest<ScheduleProposalResponse?>
    {
        public int WaterSourceId { get; set; }
        public double? TargetMoisturePercent { get; set; }
        public int TypicalDurationMinutes { get; set; } = 120;
        public double? RainThresholdMm { get; set; }
    }
}
