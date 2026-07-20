using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Pivots
{
    public class GetMoisturePredictionRequest : IRequest<MoisturePredictionResponse?>
    {
        public int PivotId { get; set; }
        public int UserId { get; set; }
    }
}
