using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class GetListPivotByUserIdRequest : IRequest<IAsyncEnumerable<GetPivotResponse>>
    {
        public int? UserId { get; set; }
    }
}
