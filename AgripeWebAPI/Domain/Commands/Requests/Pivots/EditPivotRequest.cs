using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Pivots
{
    public class EditPivotRequest : IRequest<EditPivotResponse>
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
