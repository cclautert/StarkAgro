using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.WaterSources
{
    public class DeleteWaterSourceRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }
}
