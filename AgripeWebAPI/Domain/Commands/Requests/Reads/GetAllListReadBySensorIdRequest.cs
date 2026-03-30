using AgripeWebAPI.Domain.Commands.Responses.Reads;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Reads
{
    public class GetAllListReadBySensorIdRequest : IRequest<IAsyncEnumerable<GetAllReadBySensorIdResponse>>
    {
        public int? SensorId { get; set; }
        public int? Quadrante { get; set; } = 0;
        public int NumberOfReads { get; set; } = 10;
    }
}
