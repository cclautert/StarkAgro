using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.WaterSources
{
    public class EditWaterSourceRequest : IRequest<WaterSourceResponse?>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<int> PivotIds { get; set; } = new();
        public double MaxFlowLitersPerHour { get; set; }
    }
}
