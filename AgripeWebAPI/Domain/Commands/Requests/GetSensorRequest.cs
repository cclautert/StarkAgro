using AgripeWebAPI.Domain.Commands.Responses;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests
{
    public class GetSensorRequest : IRequest<GetSensorResponse>    
    {
        public string Id { get; set; }
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
    }
}
