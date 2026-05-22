using AgripeWebAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Users
{
    public class EditUserLimitsRequest : IRequest<EditUserResponse>
    {
        public int Id { get; set; }
        public decimal LimiteInferior { get; set; }
        public decimal LimiteSuperior { get; set; }
        public double? RainThresholdMm { get; set; }
    }
}
