using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class EditPivotRequest : IRequest<EditPivotResponse>
    {
        public string Name { get; set; }
    }
}
