using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.WaterSources
{
    public class CreateWaterSourceRequest : IRequest<WaterSourceResponse?>
    {
        public string Name { get; set; } = string.Empty;
        public List<int> PivotIds { get; set; } = new();
        public double MaxFlowLitersPerHour { get; set; }
    }
}
