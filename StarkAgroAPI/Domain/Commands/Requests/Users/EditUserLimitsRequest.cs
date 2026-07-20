using StarkAgroAPI.Domain.Commands.Responses.Users;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Users
{
    public class EditUserLimitsRequest : IRequest<EditUserResponse>
    {
        public int Id { get; set; }
        public decimal LimiteInferior { get; set; }
        public decimal LimiteSuperior { get; set; }
        public double? RainThresholdMm { get; set; }
        public int? UplinkIntervalSeconds { get; set; }
    }
}
