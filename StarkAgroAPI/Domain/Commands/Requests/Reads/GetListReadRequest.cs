using StarkAgroAPI.Domain.Commands.Responses.Reads;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Reads
{
    public class GetListReadRequest : IRequest<IAsyncEnumerable<GetReadResponse>>
    {
        public int? UserId { get; set; }
        public int NumberOfReads { get; set; } = 10;
    }
}
