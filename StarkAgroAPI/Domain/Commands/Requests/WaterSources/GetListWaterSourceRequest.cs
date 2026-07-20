using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.WaterSources
{
    public class GetListWaterSourceRequest : IRequest<List<WaterSourceResponse>>
    {
    }
}
