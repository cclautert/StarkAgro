using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.WaterSources
{
    public class GetWaterSourceRequest : IRequest<WaterSourceResponse?>
    {
        public int Id { get; set; }
    }
}
