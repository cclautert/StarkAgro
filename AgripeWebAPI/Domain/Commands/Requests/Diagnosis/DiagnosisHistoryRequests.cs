using AgripeWebAPI.Domain.Commands.Responses.Diagnosis;
using MediatR;

namespace AgripeWebAPI.Domain.Commands.Requests.Diagnosis
{
    /// <summary>PDF do laudo. Passa pela mesma regra de acesso: dono ou agrônomo vinculado.</summary>
    public class GetDiagnosisPdfRequest : IRequest<DiagnosisPdfResponse?>
    {
        public int Id { get; set; }
    }

    /// <summary>Reenfileira um laudo que falhou, sem exigir que o produtor reenvie a foto.</summary>
    public class ReprocessDiagnosisRequest : IRequest<bool>
    {
        public int Id { get; set; }
    }

    /// <summary>Histórico do talhão: os laudos daquele pivô, em ordem, para ver a evolução.</summary>
    public class GetDiagnosisHistoryRequest : IRequest<DiagnosisHistoryResponse>
    {
        public int PivotId { get; set; }
    }

    public class GetDiagnosisAuditRequest : IRequest<List<DiagnosisAuditEntryResponse>?>
    {
        public int Id { get; set; }
    }
}
