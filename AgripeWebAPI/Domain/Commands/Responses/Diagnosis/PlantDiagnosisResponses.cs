namespace AgripeWebAPI.Domain.Commands.Responses.Diagnosis
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
}
