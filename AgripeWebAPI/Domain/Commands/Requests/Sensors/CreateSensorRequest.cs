using System.ComponentModel.DataAnnotations;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Validators;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Sensors
{
    public class CreateSensorRequest : IRequest<CreateSensorResponse>
    {
        public string? Name { get; set; }
        public Pivot? Pivot { get; set; }
        public int? UserId { get; set; }
        [Required]
        [MacAddress]
        public string? Code { get; set; }
        public int Quadrante { get; set; }
    }
}
