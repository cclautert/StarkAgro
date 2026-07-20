using StarkAgroAPI.Domain.Commands.Responses.Reads;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Reads
{
    public class GetListReadByPivotIdRequest : IRequest<GetReadByPivotIdResponse>
    {
        public int? PivotId { get; set; }
        public int NumberOfReads { get; set; } = 10;
        public int UserId { get; set; }
    }
}
