using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Domain.Commands.Responses.Sensor
{
    public class GetReadResponse
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public Decimal Value { get; set; }
        public DateTime Date { get; set; }        
    }
}
