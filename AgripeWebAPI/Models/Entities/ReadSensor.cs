using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public class ReadSensor
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("Sensor")]
        public int SensorId { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public string? Name { get; set; }
        public Decimal Value { get; set; }
        public DateTime Date { get; set; }
    }
}
