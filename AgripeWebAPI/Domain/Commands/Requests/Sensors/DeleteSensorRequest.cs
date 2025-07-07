using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class DeleteSensorRequest: IRequest<DeleteSensorResponse>
    {
        public int Id { get; set; }
    }
}
