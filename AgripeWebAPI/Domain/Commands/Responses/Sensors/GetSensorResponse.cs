using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models.Entities;

namespace AgripeWebAPI.Domain.Commands.Responses.Sensors
{
    public class GetSensorResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Pivot Pivot { get; set; }
        public int Quadrante { get; set; }
        public string? Code { get; set; }
    }
}
