using System.ComponentModel.DataAnnotations;
using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using StarkAgroAPI.Models.Entities;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Sensors
{
    public class CreateSensorRequest : IRequest<CreateSensorResponse>
    {
        public string? Name { get; set; }
        public Pivot? Pivot { get; set; }
        public int? UserId { get; set; }
        [Required]
        public string? Code { get; set; }
        public int Quadrante { get; set; }
        public int? UplinkIntervalSeconds { get; set; } = 10800;
    }
}
