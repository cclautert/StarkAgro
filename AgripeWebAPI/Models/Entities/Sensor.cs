using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public class Sensor : Entity
    {
        [ForeignKey("Pivot")]
        public int PivoId { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public int Quadrante { get; set; }
        public string? Name { get; set; } = null;
        public string? Code { get; set; } = null;

        public Pivot? Pivot { get; set; }
        public User? User { get; set; }
        public ICollection<ReadSensor>? Reads { get; set; }
    }
}
