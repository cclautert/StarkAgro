namespace AgripeWebAPI.Domain.Commands.Responses.Reads
{
    public class CreateReadResponse
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; }
    }
}
