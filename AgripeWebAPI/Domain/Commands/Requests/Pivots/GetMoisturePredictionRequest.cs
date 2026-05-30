using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class GetMoisturePredictionRequest : IRequest<MoisturePredictionResponse?>
    {
        public int PivotId { get; set; }
        public int UserId { get; set; }
    }
}
