namespace AgripeWebAPI.Domain.Commands.Responses
{
    public class GetSensorResponse
    {
        public string Id { get; set; }
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
    }
}
