using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class GetPivotForecastRequest : IRequest<GetPivotForecastResponse?>
    {
        public int? PivotId { get; set; }
        public int? Days { get; set; }
        public int UserId { get; set; }
    }
}
