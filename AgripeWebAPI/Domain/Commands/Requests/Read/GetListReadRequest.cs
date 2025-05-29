using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensor
{
    public class GetListReadRequest : IRequest<IList<GetReadResponse>>
    {
        public int UserId { get; set; }
        public int NumberOfReads { get; set; } = 10;
    }
}
