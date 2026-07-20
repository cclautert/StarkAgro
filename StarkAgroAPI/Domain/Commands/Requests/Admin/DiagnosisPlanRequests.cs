using StarkAgroAPI.Domain.Commands.Responses.Admin;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Admin
{
    public class GetDiagnosisPlansRequest : IRequest<List<DiagnosisPlanResponse>>
    {
    }

    public class CreateDiagnosisPlanRequest : IRequest<DiagnosisPlanResponse>
    {
        [Required]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "O nome do plano deve ter entre 1 e 80 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Range(0, 100_000_000, ErrorMessage = "MonthlyPriceCents deve ser >= 0.")]
        public int MonthlyPriceCents { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "IncludedReportsPerMonth deve ser >= 0.")]
        public int IncludedReportsPerMonth { get; set; }

        [Range(0, 100_000_000, ErrorMessage = "OveragePriceCents deve ser >= 0.")]
        public int OveragePriceCents { get; set; }

        public bool Active { get; set; } = true;
    }

    public class UpdateDiagnosisPlanRequest : IRequest<DiagnosisPlanResponse>
    {
        public int Id { get; set; }

        [Required]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "O nome do plano deve ter entre 1 e 80 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Range(0, 100_000_000, ErrorMessage = "MonthlyPriceCents deve ser >= 0.")]
        public int MonthlyPriceCents { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "IncludedReportsPerMonth deve ser >= 0.")]
        public int IncludedReportsPerMonth { get; set; }

        [Range(0, 100_000_000, ErrorMessage = "OveragePriceCents deve ser >= 0.")]
        public int OveragePriceCents { get; set; }

        public bool Active { get; set; } = true;
    }

    public class DeleteDiagnosisPlanRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }
}
