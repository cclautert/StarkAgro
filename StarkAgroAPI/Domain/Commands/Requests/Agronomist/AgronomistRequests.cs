using StarkAgroAPI.Domain.Commands.Responses.Agronomist;
using StarkAgroAPI.Domain.Commands.Responses.Diagnosis;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace StarkAgroAPI.Domain.Commands.Requests.Agronomist
{
    public class GetAgronomistBillingRequest : IRequest<AgronomistBillingResponse>
    {
    }

    public class GetAgronomistQueueRequest : IRequest<List<AgronomistQueueItemResponse>>
    {
        public string? Status { get; set; }
        public int PageSize { get; set; } = 20;
        public int PageIndex { get; set; } = 0;
    }

    /// <summary>Detalhe do laudo pela ótica do agrônomo (inclui o contexto congelado).</summary>
    public class GetAgronomistDiagnosisRequest : IRequest<PlantDiagnosisResponse?>
    {
        public int Id { get; set; }
    }

    public class GetAgronomistDiagnosisImageRequest : IRequest<PlantDiagnosisImageResponse?>
    {
        public int Id { get; set; }
    }

    /// <summary>Assume o laudo: PendingReview → InReview.</summary>
    public class ClaimDiagnosisRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }

    /// <summary>Salva o rascunho da revisão, sem assinar.</summary>
    public class ReviewDiagnosisRequest : IRequest<bool>
    {
        public int Id { get; set; }
        public string? ReportMarkdown { get; set; }
        public string? ConfirmedDisease { get; set; }
        public string? Severity { get; set; }
        public string? Prescription { get; set; }
    }

    public class SignDiagnosisRequest : IRequest<bool>
    {
        public int Id { get; set; }

        [Required]
        public string ReportMarkdown { get; set; } = string.Empty;

        public string? ConfirmedDisease { get; set; }
        public string? Severity { get; set; }
        public string? Prescription { get; set; }
        public string? Crea { get; set; }
    }

    public class RejectDiagnosisRequest : IRequest<bool>
    {
        public int Id { get; set; }

        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    public class GetAgronomistClientsRequest : IRequest<List<AgronomistClientResponse>>
    {
    }

    public class InviteClientRequest : IRequest<AgronomistClientResponse?>
    {
        [Required]
        [EmailAddress]
        public string ClientEmail { get; set; } = string.Empty;
    }

    public class RevokeClientRequest : IRequest<bool>
    {
        public int LinkId { get; set; }
    }

    // ── Lado do produtor ──────────────────────────────────────────────────────

    public class GetMyAgronomistInvitesRequest : IRequest<List<AgronomistInviteResponse>>
    {
    }

    public class AcceptAgronomistInviteRequest : IRequest<bool>
    {
        public int InviteId { get; set; }
    }

    public class DeclineAgronomistInviteRequest : IRequest<bool>
    {
        public int InviteId { get; set; }
    }

    /// <summary>O produtor demite o agrônomo. Direito irrevogável dele.</summary>
    public class RevokeMyAgronomistRequest : IRequest<bool>
    {
    }
}
