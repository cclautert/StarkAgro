namespace StarkAgroAPI.Domain.Commands.Responses.Diagnosis
{
    public class CreatePlantDiagnosisResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusUrl { get; set; } = string.Empty;
    }

    /// <summary>Item da listagem — sem o markdown do laudo, que só vem no detalhe.</summary>
    public class PlantDiagnosisSummaryResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? PivotId { get; set; }
        public string? CropName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? FailureReason { get; set; }
    }

    public class DiseaseSuggestionResponse
    {
        public string Name { get; set; } = string.Empty;
        public string? ScientificName { get; set; }
        public double Probability { get; set; }
        public string? Severity { get; set; }
        public string? Symptoms { get; set; }
        public List<string> Treatments { get; set; } = [];
    }

    public class DiagnosisSignatureResponse
    {
        public string AgronomistName { get; set; } = string.Empty;
        public string? Crea { get; set; }
        public DateTime SignedAt { get; set; }
        public string ContentSha256 { get; set; } = string.Empty;
    }

    /// <summary>Dados da lavoura congelados no laudo — é o que o agrônomo lerá na Fase 2.</summary>
    public class DiagnosisContextResponse
    {
        public string? PivotName { get; set; }
        public decimal? MoistureAvg7d { get; set; }
        public decimal? LimiteInferior { get; set; }
        public decimal? LimiteSuperior { get; set; }
        public int DaysAboveUpperLimit { get; set; }
        public int OpenAnomalies { get; set; }
        public int IrrigationAlerts7d { get; set; }
        public string? ForecastSummary { get; set; }
    }

    public class PlantDiagnosisResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? PivotId { get; set; }
        public string? CropName { get; set; }
        public string? ProducerNotes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime CapturedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public bool IsPlant { get; set; }
        public double TopProbability { get; set; }
        public List<DiseaseSuggestionResponse> Diseases { get; set; } = [];
        public DiagnosisContextResponse? Context { get; set; }
        public string? AiReportMarkdown { get; set; }
        public string? AiProvider { get; set; }

        /// <summary>Nome do produtor — só preenchido na visão do agrônomo.</summary>
        public string? ClientName { get; set; }

        public string? AgronomistReportMarkdown { get; set; }
        public string? ConfirmedDisease { get; set; }
        public string? AgronomistSeverity { get; set; }
        public string? Prescription { get; set; }
        public string? RejectionReason { get; set; }
        public DiagnosisSignatureResponse? Signature { get; set; }

        public string? FailureReason { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class PlantDiagnosisStatusResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string? FailureReason { get; set; }
    }

    public class PlantDiagnosisImageResponse
    {
        public byte[] Content { get; set; } = [];
        public string ContentType { get; set; } = string.Empty;
    }

    public class DiagnosisPdfResponse
    {
        public byte[] Content { get; set; } = [];
        public string FileName { get; set; } = string.Empty;
    }

    public class DiagnosisQuotaResponse
    {
        /// <summary>Laudos permitidos no mês. <c>0</c> = ilimitado.</summary>
        public int Limit { get; set; }
        public int Used { get; set; }
        public int Remaining { get; set; }
        public bool IsUnlimited { get; set; }
        public bool IsExhausted { get; set; }
        public DateTime ResetsAt { get; set; }
    }

    public class DiagnosisAuditEntryResponse
    {
        public DateTime At { get; set; }
        public int? ActorUserId { get; set; }
        public string? ActorName { get; set; }
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }

    /// <summary>Um laudo na linha do tempo do talhão.</summary>
    public class DiagnosisHistoryItemResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CapturedAt { get; set; }
        public string? TopDisease { get; set; }
        public double TopProbability { get; set; }
        public string? ConfirmedDisease { get; set; }
        public string? Severity { get; set; }
        public decimal? MoistureAvg7d { get; set; }
        public int DaysAboveUpperLimit { get; set; }
        public bool IsSigned { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class DiagnosisHistoryResponse
    {
        public int PivotId { get; set; }
        public string? PivotName { get; set; }
        public List<DiagnosisHistoryItemResponse> Items { get; set; } = [];

        /// <summary>
        /// Leitura da evolução entre o laudo mais antigo e o mais recente do mesmo talhão —
        /// é o que responde "a mancha piorou?".
        /// </summary>
        public string? Trend { get; set; }
    }
}
