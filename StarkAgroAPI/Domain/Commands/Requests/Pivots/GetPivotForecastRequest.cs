using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class GetPivotForecastRequest : IRequest<GetPivotForecastResponse?>
    {
        public int? PivotId { get; set; }
        public int? Days { get; set; }
        public int UserId { get; set; }
    }
}
