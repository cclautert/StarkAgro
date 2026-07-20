using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class GetPivotRequest : IRequest<GetPivotResponse>
    {
        public int Id { get; set; }
    }
}
