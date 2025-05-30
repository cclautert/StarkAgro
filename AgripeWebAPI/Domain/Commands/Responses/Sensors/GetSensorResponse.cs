namespace AgripeWebAPI.Domain.Commands.Responses.Sensors
{
    public class GetSensorResponse
    {
        public int Id { get; set; }
        public int PivoId { get; set; }        
        public int UserId { get; set; }
        public int Quadrante { get; set; }
        public string? Code { get; set; }
    }
}
