using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class DeletePivotRequest : IRequest<DeletePivotResponse>
    {
        public int Id { get; set; }
    }
}
