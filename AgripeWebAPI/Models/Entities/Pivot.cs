using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public class Pivot : Entity
    { 
        [ForeignKey("User")]
        public int UserId { get; set; }

        public string? Name { get; set; }
        
        public ICollection<Sensor>? Sensors { get; set; }
    }
}
