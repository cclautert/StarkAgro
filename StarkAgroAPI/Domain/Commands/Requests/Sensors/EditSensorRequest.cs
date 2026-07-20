using System.ComponentModel.DataAnnotations;
using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using StarkAgroAPI.Models.Entities;
using MediatR;

namespace StarkAgroAPI.Domain.Commands.Requests.Sensors
{
    public class EditSensorRequest : IRequest<EditSensorResponse>
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Pivot? Pivot { get; set; }
        public int? UserId { get; set; }
        public string? Code { get; set; }
        public int Quadrante { get; set; }
        public int? UplinkIntervalSeconds { get; set; }
    }
}
