using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class EditPivotLimitsRequest : IRequest<EditPivotResponse>
    {
        public int Id { get; set; }
        public decimal? LimiteInferior { get; set; }
        public decimal? LimiteSuperior { get; set; }
        public double? RainThresholdMm { get; set; }
    }
}
