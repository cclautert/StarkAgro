using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class GetPivotRequest : IRequest<GetPivotResponse>
    {
        public int Id { get; set; }
    }
}
