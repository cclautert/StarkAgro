namespace AgripeWebAPI.Domain.Commands.Responses.Users
{
    public class GetUserResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public decimal LimiteInferior { get; set; }
        public decimal LimiteSuperior { get; set; }
        public double? RainThresholdMm { get; set; }
    }
}
