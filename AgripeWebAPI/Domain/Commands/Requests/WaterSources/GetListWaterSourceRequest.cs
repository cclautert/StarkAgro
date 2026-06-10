using AgripeWebAPI.Domain.Commands.Responses.WaterSources;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.WaterSources
{
    public class GetListWaterSourceRequest : IRequest<List<WaterSourceResponse>>
    {
    }
}
