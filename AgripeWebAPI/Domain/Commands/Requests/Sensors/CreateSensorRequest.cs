using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models.Entities;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class CreateSensorRequest : IRequest<CreateSensorResponse>
    {
        public Pivot? Pivot { get; set; }
        public int? UserId { get; set; }
        public string Code { get; set; }
        public int Quadrante { get; set; }
    }
}
