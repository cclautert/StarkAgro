using AgripeWebAPI.Domain.Commands.Responses.Reads;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Reads
{
    public class CreateReadRequest : IRequest<CreateReadResponse>
    {
        public string Code { get; set; }
        public decimal Value { get; set; }
    }
}
