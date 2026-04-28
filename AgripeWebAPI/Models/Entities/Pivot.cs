using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public class Pivot : Entity
    {
        [ForeignKey("User")]
        public int UserId { get; set; }

        public string? Name { get; set; }

        public decimal? LimiteInferior { get; set; }

        public decimal? LimiteSuperior { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double? Altitude { get; set; }

        public string? LocationAddress { get; set; }

        public DateTime? LocationUpdatedAt { get; set; }

        public ICollection<Sensor>? Sensors { get; set; }
    }
}
