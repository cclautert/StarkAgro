namespace AgripeWebAPI.Domain.Commands.Responses.Users
{
    public class GetUserResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal LimiteInferior { get; set; }
        public decimal LimiteSuperior { get; set; }
        public double? RainThresholdMm { get; set; }
    }
}
