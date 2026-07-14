using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AgripeWebAPI.Models.Entities
{
    /// <summary>
    /// Estados possíveis de um laudo fitossanitário.
    /// <para>
    /// Uploaded → Processing → (PendingReview | AiCompleted | Rejected | Failed)
    /// e, a partir da Fase 2, PendingReview → InReview → (Signed | Rejected).
    /// </para>
    /// <para>
    /// AiCompleted, Signed, Rejected e Failed são terminais.
    /// </para>
    /// </summary>
    public static class PlantDiagnosisStatus
    {
        public const string Uploaded = "Uploaded";
        public const string Processing = "Processing";
        public const string PendingReview = "PendingReview";
        public const string InReview = "InReview";
        public const string AiCompleted = "AiCompleted";
        public const string Signed = "Signed";
        public const string Rejected = "Rejected";
        public const string Failed = "Failed";

        public static readonly string[] Terminal =
            [AiCompleted, Signed, Rejected, Failed];

        public static bool IsTerminal(string status) => Terminal.Contains(status);
    }

    /// <summary>
    /// Registro append-only de cada transição do laudo. É o que sustenta a auditoria
    /// de um documento que um agrônomo assina.
    /// </summary>
    public class PlantDiagnosisAuditEntry
    {
        public DateTime At { get; set; }
        public int? ActorUserId { get; set; }
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }

    /// <summary>Doença sugerida pelo classificador, com a probabilidade que ele atribuiu.</summary>
    public class PlantDiseaseSuggestion
    {
        public string Name { get; set; } = string.Empty;
        public string? ScientificName { get; set; }
        public double Probability { get; set; }
        public string? Severity { get; set; }
        public string? Symptoms { get; set; }
        public List<string> Treatments { get; set; } = [];
    }

    /// <summary>
    /// Retrato dos dados da lavoura no momento do laudo: umidade, anomalias, irrigação e clima.
    /// <para>
    /// É <b>congelado dentro do documento</b> de propósito. Dá reprodutibilidade jurídica (o laudo
    /// assinado reflete os dados do dia, não os de hoje) e, a partir da Fase 2, é o que permite ao
    /// agrônomo ver o contexto do cliente <b>sem ler as coleções dele</b>.
    /// </para>
    /// </summary>
    public class PlantDiagnosisContextSnapshot
    {
        public string? PivotName { get; set; }
        public decimal? MoistureAvg7d { get; set; }
        public decimal? LimiteInferior { get; set; }
        public decimal? LimiteSuperior { get; set; }
        public int DaysAboveUpperLimit { get; set; }
        public List<SensorReadingSnapshot> LastReadings { get; set; } = [];
        public int OpenAnomalies { get; set; }
        public int IrrigationAlerts7d { get; set; }
        public string? ForecastSummary { get; set; }
        public DateTime CapturedAt { get; set; }
    }

    public class SensorReadingSnapshot
    {
        public string? SensorCode { get; set; }
        public int? Quadrante { get; set; }
        public decimal? Humidity { get; set; }
        public decimal? Temperature { get; set; }
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// Laudo fitossanitário: uma foto de planta enviada pelo produtor, a pré-análise
    /// da IA e (a partir da Fase 2) a revisão e assinatura do agrônomo.
    /// </summary>
    public class PlantDiagnosis : Entity
    {
        /// <summary>Produtor dono do laudo. É o tenant — nunca vem do request.</summary>
        public int UserId { get; set; }

        public int? PivotId { get; set; }
        public string? CropName { get; set; }
        public string? ProducerNotes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime CapturedAt { get; set; }

        /// <summary>Referência ao arquivo no bucket GridFS <c>diagnosis_images</c>.</summary>
        public ObjectId ImageFileId { get; set; }
        public string ImageContentType { get; set; } = string.Empty;
        public long ImageSizeBytes { get; set; }

        /// <summary>SHA-256 da imagem — usado para deduplicar reenvios da mesma foto.</summary>
        public string ImageSha256 { get; set; } = string.Empty;

        public string Status { get; set; } = PlantDiagnosisStatus.Uploaded;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ProcessingStartedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        /// <summary>Quando o worker pode tentar processar de novo (backoff entre retentativas).</summary>
        public DateTime NextAttemptAt { get; set; }
        public int RetryCount { get; set; }
        public string? FailureReason { get; set; }
        public string? WorkerId { get; set; }

        // ── Classificador (Kindwise) ──────────────────────────────────────────
        public bool IsPlant { get; set; } = true;
        public double TopProbability { get; set; }
        public List<PlantDiseaseSuggestion> Diseases { get; set; } = [];

        /// <summary>Resposta crua do classificador — auditoria de um documento que será assinado.</summary>
        [BsonIgnoreIfNull]
        public string? CropHealthRawJson { get; set; }
        public string? CropHealthProvider { get; set; }

        // ── Laudo (LLM) ───────────────────────────────────────────────────────
        /// <summary>
        /// Laudo redigido pela IA. <b>Imutável</b> — a edição do agrônomo vive em outro campo
        /// (Fase 2), para que se possa auditar o que a IA disse versus o que ele assinou.
        /// </summary>
        public string? AiReportMarkdown { get; set; }
        public string? AiProvider { get; set; }
        public string? AiModel { get; set; }
        public DateTime? AiGeneratedAt { get; set; }

        /// <summary>Dados da lavoura congelados no momento do processamento.</summary>
        [BsonIgnoreIfNull]
        public PlantDiagnosisContextSnapshot? ContextSnapshot { get; set; }

        [BsonIgnoreIfNull]
        public List<PlantDiagnosisAuditEntry> AuditTrail { get; set; } = [];
    }
}
