using AgripeWebAPI.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public class ReadSensor : Entity
    {
        [ForeignKey("Sensor")]
        public int SensorId { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public Decimal Value { get; set; }
        public DateTime Date { get; set; }

        public virtual Sensor? Sensor { get; set; }
    }
}
