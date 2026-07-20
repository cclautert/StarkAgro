using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class GetListPivotRequest : IRequest<IList<GetPivotResponse>>
    {
        public int Id { get; set; }
    }
}
