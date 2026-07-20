using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Sensors
{
    public class GetSensorRequest : IRequest<GetSensorResponse>
    {
        public int Id { get; set; }
    }
}
