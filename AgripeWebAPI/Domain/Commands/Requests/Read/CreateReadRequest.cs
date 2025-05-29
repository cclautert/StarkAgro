using AgripeWebAPI.Domain.Commands.Responses.Read;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Read
{
    public class CreateReadRequest : IRequest<CreateReadResponse>
    {
        public string Code { get; set; }
        public decimal Value { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
    }
}
