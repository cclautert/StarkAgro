using AgripeWebAPI.Domain.Commands.Responses.WaterSources;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.WaterSources
{
    public class GetWaterSourceRequest : IRequest<WaterSourceResponse?>
    {
        public int Id { get; set; }
    }
}
