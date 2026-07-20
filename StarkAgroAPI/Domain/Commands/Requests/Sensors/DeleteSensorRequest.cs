using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Sensors
{
    public class DeleteSensorRequest: IRequest<DeleteSensorResponse>
    {
        public int Id { get; set; }
    }
}
