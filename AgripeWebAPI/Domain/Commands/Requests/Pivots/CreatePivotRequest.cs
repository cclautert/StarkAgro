using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class CreatePivotRequest : IRequest<CreatePivotResponse>
    {
        public int? UserId { get; set; }
        public string Name { get; set; }
    }
}
