using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public class Pivot : Entity
    {        
        public string? Name { get; set; }
        
        public ICollection<Sensor>? Reads { get; set; }
    }
}
