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
