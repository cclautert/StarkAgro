using StarkAgroAPI.Domain.Commands.Responses.Admin;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    public class GetRevendasRequest : IRequest<List<RevendaResponse>>
    {
    }

    public class CreateRevendaRequest : IRequest<RevendaResponse>
    {
        [Required]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "O nome da revenda deve ter entre 1 e 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(18, ErrorMessage = "CNPJ inválido.")]
        public string? Cnpj { get; set; }

        [EmailAddress(ErrorMessage = "Email de contato inválido.")]
        public string? ContactEmail { get; set; }

        public int? DiagnosisPlanId { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "DiagnosisQuotaPerMonth deve ser >= 0.")]
        public int? DiagnosisQuotaPerMonth { get; set; }

        public bool Active { get; set; } = true;
    }

    public class UpdateRevendaRequest : IRequest<RevendaResponse>
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120, MinimumLength = 1, ErrorMessage = "O nome da revenda deve ter entre 1 e 120 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(18, ErrorMessage = "CNPJ inválido.")]
        public string? Cnpj { get; set; }

        [EmailAddress(ErrorMessage = "Email de contato inválido.")]
        public string? ContactEmail { get; set; }

        public int? DiagnosisPlanId { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "DiagnosisQuotaPerMonth deve ser >= 0.")]
        public int? DiagnosisQuotaPerMonth { get; set; }

        public bool Active { get; set; } = true;
    }

    public class AssignRevendaManagerRequest : IRequest<RevendaResponse>
    {
        /// <summary>Preenchido pela rota; não vem do corpo.</summary>
        public int RevendaId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "UserId é obrigatório.")]
        public int UserId { get; set; }
    }
}
