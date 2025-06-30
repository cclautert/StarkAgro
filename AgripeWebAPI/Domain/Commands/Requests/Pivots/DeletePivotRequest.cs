using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class DeletePivotRequest : IRequest<DeletePivotResponse>
    {
        public int Id { get; set; }
    }
}
