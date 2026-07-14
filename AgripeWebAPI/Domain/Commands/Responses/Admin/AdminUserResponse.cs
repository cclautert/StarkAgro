namespace AgripeWebAPI.Domain.Commands.Responses.Admin
{
    public class AdminUserResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsAgronomist { get; set; }
        public string? AgronomistCrea { get; set; }
        public int? DiagnosisQuotaPerMonth { get; set; }
        public decimal LimiteInferior { get; set; }
        public decimal LimiteSuperior { get; set; }
        public double? RainThresholdMm { get; set; }
        public int? UplinkIntervalSeconds { get; set; }
    }
}
