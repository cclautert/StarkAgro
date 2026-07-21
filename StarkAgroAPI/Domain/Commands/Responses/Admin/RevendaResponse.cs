namespace StarkAgroAPI.Domain.Commands.Responses.Admin
{
    public class RevendaResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Cnpj { get; set; }
        public string? ContactEmail { get; set; }
        public int? DiagnosisPlanId { get; set; }
        public int? DiagnosisQuotaPerMonth { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
