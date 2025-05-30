using AgripeWebAPI.Domain.Commands.Responses.Reads;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Reads
{
    public class GetListReadRequest : IRequest<IList<GetReadResponse>>
    {
        public int UserId { get; set; }
        public int NumberOfReads { get; set; } = 10;
    }
}
