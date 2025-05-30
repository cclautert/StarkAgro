using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class CreateSensorRequest : IRequest<CreateSensorResponse>
    {
        public int PivoId { get; set; }
        public int UserId { get; set; }
        public string Code { get; set; }
        public int Quadrante { get; set; }
    }
}
