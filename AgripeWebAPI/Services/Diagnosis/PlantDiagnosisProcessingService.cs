using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.AIInsights;
using AgripeWebAPI.Services.CropHealth;
using MongoDB.Driver;

namespace AgripeWebAPI.Services.Diagnosis
{
    public enum DiagnosisProcessingOutcome
    {
        Completed,
        RejectedLowConfidence,
        Failed
    }

    public record DiagnosisProcessingResult(DiagnosisProcessingOutcome Outcome, string? Reason = null);

    public interface IPlantDiagnosisProcessingService
    {
        Task<DiagnosisProcessingResult> ProcessAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Roda a pré-análise de um laudo: classificador → contexto da lavoura → LLM redige o laudo.
    /// <para>
    /// <b>Deliberadamente NÃO é um handler MediatR.</b> O assembly scan registraria o handler
    /// automaticamente e bastaria alguém mapeá-lo num controller para virar um IDOR perfeito
    /// (um id de laudo sem checagem de dono). Como serviço explícito fora do pipeline, fica claro
    /// que isto roda em contexto de sistema — e a regra da casa ("todo handler MediatR filtra por
    /// _currentUser.UserId") continua verdadeira e auditável por grep.
    /// </para>
    /// <para>
    /// Por isso também <b>não injeta ICurrentUserContext</b>: no worker ele devolve
    /// <c>UserId = null</c>. O tenant vem de dentro do documento (<c>diagnosis.UserId</c>).
    /// </para>
    /// </summary>
    public class PlantDiagnosisProcessingService : IPlantDiagnosisProcessingService
    {
        /// <summary>
        /// Abaixo disto a evidência é fraca demais para virar laudo. Um laudo confiante em cima de
        /// foto ruim destrói a confiança no produto mais rápido do que laudo nenhum.
        /// </summary>
        private const double MinimumProbability = 0.25;

        private const string LowConfidenceHint =
            "Não foi possível identificar a doença com segurança nesta foto. " +
            "Aproxime a folha, foque na lesão, use luz natural e evite contraluz.";

        private const string NotAPlantHint =
            "A imagem não parece ser de uma planta. Fotografe a folha ou o caule com o sintoma.";

        /// <summary>
        /// O laudo tem 6 seções e não cabe nos 1024 tokens que servem a um insight curto —
        /// com o teto padrão ele é cortado no meio, levando junto o disclaimer do rodapé.
        /// </summary>
        private const int ReportMaxTokens = 3000;

        public const string Disclaimer =
            "_Laudo técnico informativo. Não constitui receituário agronômico nem ART._";

        private readonly agpDBContext _dbContext;
        private readonly ICropDiagnosisProvider _cropDiagnosisProvider;
        private readonly IPlantDiagnosisContextBuilder _contextBuilder;
        private readonly IAIInsightsServiceFactory _aiServiceFactory;
        private readonly IDiagnosisImageStore _imageStore;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<PlantDiagnosisProcessingService> _logger;

        public PlantDiagnosisProcessingService(
            agpDBContext dbContext,
            ICropDiagnosisProvider cropDiagnosisProvider,
            IPlantDiagnosisContextBuilder contextBuilder,
            IAIInsightsServiceFactory aiServiceFactory,
            IDiagnosisImageStore imageStore,
            IPushNotificationService pushService,
            ILogger<PlantDiagnosisProcessingService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cropDiagnosisProvider = cropDiagnosisProvider ?? throw new ArgumentNullException(nameof(cropDiagnosisProvider));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _aiServiceFactory = aiServiceFactory ?? throw new ArgumentNullException(nameof(aiServiceFactory));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
            _pushService = pushService ?? throw new ArgumentNullException(nameof(pushService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DiagnosisProcessingResult> ProcessAsync(
            PlantDiagnosis diagnosis,
            CancellationToken cancellationToken)
        {
            var settings = await _dbContext.PlatformAiSettings
                .Find(_ => true)
                .FirstOrDefaultAsync(cancellationToken);

            if (settings is null || !settings.CropHealthEnabled || string.IsNullOrWhiteSpace(settings.CropHealthKey))
            {
                return new DiagnosisProcessingResult(
                    DiagnosisProcessingOutcome.Failed,
                    "Análise por IA não está habilitada. Contate o administrador.");
            }

            var image = await _imageStore.DownloadAsync(diagnosis.ImageFileId, cancellationToken);
            if (image is null)
            {
                return new DiagnosisProcessingResult(
                    DiagnosisProcessingOutcome.Failed, "A foto do laudo não foi encontrada.");
            }

            // 1) Classificador especializado — é ele que decide qual é a doença.
            var cropResult = await _cropDiagnosisProvider.IdentifyAsync(
                new CropDiagnosisInput
                {
                    ImageBytes = image,
                    ContentType = diagnosis.ImageContentType,
                    Latitude = diagnosis.Latitude,
                    Longitude = diagnosis.Longitude,
                    CapturedAt = diagnosis.CapturedAt
                },
                settings.CropHealthKey,
                cancellationToken);

            if (cropResult is null)
            {
                return new DiagnosisProcessingResult(
                    DiagnosisProcessingOutcome.Failed, "O serviço de diagnóstico não respondeu.");
            }

            if (!cropResult.IsPlant)
            {
                await RejectAsync(diagnosis, cropResult, NotAPlantHint, cancellationToken);
                return new DiagnosisProcessingResult(DiagnosisProcessingOutcome.RejectedLowConfidence, NotAPlantHint);
            }

            if (cropResult.TopProbability < MinimumProbability)
            {
                await RejectAsync(diagnosis, cropResult, LowConfidenceHint, cancellationToken);
                return new DiagnosisProcessingResult(DiagnosisProcessingOutcome.RejectedLowConfidence, LowConfidenceHint);
            }

            // 2) Contexto da lavoura, congelado no documento.
            var snapshot = await _contextBuilder.BuildAsync(diagnosis, cancellationToken);

            // 3) LLM redige o laudo — não diagnostica, só escreve e correlaciona.
            var aiService = _aiServiceFactory.GetService(settings.ActiveProvider);
            if (aiService is null)
            {
                return new DiagnosisProcessingResult(
                    DiagnosisProcessingOutcome.Failed,
                    $"Provider de IA '{settings.ActiveProvider}' não é suportado.");
            }

            var (apiKey, model) = settings.ActiveProvider.ToLower() switch
            {
                "gemini" => (settings.GeminiKey, settings.GeminiModel),
                "anthropic" => (settings.AnthropicKey, settings.AnthropicModel),
                "openai" => (settings.OpenAiKey, settings.OpenAiModel),
                _ => ((string?)null, (string?)null)
            };

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new DiagnosisProcessingResult(
                    DiagnosisProcessingOutcome.Failed, "Chave da API de IA não configurada.");
            }

            var report = await aiService.CompleteAsync(
                PhytosanitaryReportPromptBuilder.SystemPrompt,
                PhytosanitaryReportPromptBuilder.BuildUserMessage(diagnosis, cropResult, snapshot),
                apiKey,
                model,
                cancellationToken,
                ReportMaxTokens);

            if (string.IsNullOrWhiteSpace(report))
            {
                // O diagnóstico do classificador já foi pago — preserva-o para a retentativa
                // não precisar chamar o Kindwise de novo.
                await SaveCropResultAsync(diagnosis, cropResult, snapshot, cancellationToken);
                return new DiagnosisProcessingResult(
                    DiagnosisProcessingOutcome.Failed, "A IA não conseguiu redigir o laudo.");
            }

            await CompleteAsync(
                diagnosis, cropResult, snapshot, EnsureDisclaimer(report), settings, model, cancellationToken);

            await NotifyAsync(
                diagnosis.UserId,
                "Laudo pronto",
                $"A pré-análise da sua foto está pronta: {cropResult.Diseases.FirstOrDefault()?.Name ?? "resultado disponível"}.",
                cancellationToken);

            return new DiagnosisProcessingResult(DiagnosisProcessingOutcome.Completed);
        }

        private async Task CompleteAsync(
            PlantDiagnosis diagnosis,
            CropDiagnosisResult cropResult,
            PlantDiagnosisContextSnapshot snapshot,
            string report,
            PlatformAiSettings settings,
            string? model,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // Sem agrônomo vinculado (Fase 2), o laudo termina em AiCompleted.
            const string nextStatus = PlantDiagnosisStatus.AiCompleted;

            var update = Builders<PlantDiagnosis>.Update
                .Set(d => d.Status, nextStatus)
                .Set(d => d.IsPlant, true)
                .Set(d => d.Diseases, ToEntity(cropResult.Diseases))
                .Set(d => d.TopProbability, cropResult.TopProbability)
                .Set(d => d.CropHealthRawJson, cropResult.RawJson)
                .Set(d => d.CropHealthProvider, "kindwise")
                .Set(d => d.ContextSnapshot, snapshot)
                .Set(d => d.AiReportMarkdown, report)
                .Set(d => d.AiProvider, settings.ActiveProvider)
                .Set(d => d.AiModel, model)
                .Set(d => d.AiGeneratedAt, now)
                .Set(d => d.ProcessedAt, now)
                .Set(d => d.UpdatedAt, now)
                .Set(d => d.FailureReason, (string?)null)
                .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                {
                    At = now,
                    FromStatus = PlantDiagnosisStatus.Processing,
                    ToStatus = nextStatus,
                    Action = "processed:ai"
                });

            await _dbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id, update, null, cancellationToken);

            _logger.LogInformation(
                "PlantDiagnosis {Id} (user {UserId}) analysed: {Disease} @ {Probability:P0}",
                diagnosis.Id, diagnosis.UserId,
                cropResult.Diseases.FirstOrDefault()?.Name, cropResult.TopProbability);
        }

        private async Task RejectAsync(
            PlantDiagnosis diagnosis,
            CropDiagnosisResult cropResult,
            string reason,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var update = Builders<PlantDiagnosis>.Update
                .Set(d => d.Status, PlantDiagnosisStatus.Rejected)
                .Set(d => d.IsPlant, cropResult.IsPlant)
                .Set(d => d.Diseases, ToEntity(cropResult.Diseases))
                .Set(d => d.TopProbability, cropResult.TopProbability)
                .Set(d => d.CropHealthRawJson, cropResult.RawJson)
                .Set(d => d.CropHealthProvider, "kindwise")
                .Set(d => d.FailureReason, reason)
                .Set(d => d.ProcessedAt, now)
                .Set(d => d.UpdatedAt, now)
                .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                {
                    At = now,
                    FromStatus = PlantDiagnosisStatus.Processing,
                    ToStatus = PlantDiagnosisStatus.Rejected,
                    Action = "rejected:low-confidence"
                });

            await _dbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id, update, null, cancellationToken);

            await NotifyAsync(diagnosis.UserId, "Foto não pôde ser analisada", reason, cancellationToken);

            _logger.LogInformation(
                "PlantDiagnosis {Id} rejected (isPlant={IsPlant}, top={Top:P0})",
                diagnosis.Id, cropResult.IsPlant, cropResult.TopProbability);
        }

        /// <summary>Guarda o resultado do classificador quando só o LLM falhou — já foi pago.</summary>
        private async Task SaveCropResultAsync(
            PlantDiagnosis diagnosis,
            CropDiagnosisResult cropResult,
            PlantDiagnosisContextSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            var update = Builders<PlantDiagnosis>.Update
                .Set(d => d.Diseases, ToEntity(cropResult.Diseases))
                .Set(d => d.TopProbability, cropResult.TopProbability)
                .Set(d => d.CropHealthRawJson, cropResult.RawJson)
                .Set(d => d.CropHealthProvider, "kindwise")
                .Set(d => d.ContextSnapshot, snapshot);

            await _dbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id, update, null, cancellationToken);
        }

        private async Task NotifyAsync(int userId, string title, string body, CancellationToken cancellationToken)
        {
            try
            {
                await _pushService.SendAsync(userId, title, body, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Push é acessório: o laudo já está gravado e aparece na tela pelo polling.
                _logger.LogWarning(ex, "Push notification failed for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Garante o aviso legal no rodapé. O prompt já pede, mas um laudo cortado por limite de
        /// tokens (ou um modelo que simplesmente não obedeceu) sairia sem ele — e é justamente a
        /// linha que separa um documento informativo de algo que parece receituário. Não pode
        /// depender da boa vontade do modelo.
        /// </summary>
        public static string EnsureDisclaimer(string report)
            => report.Contains("receituário agronômico", StringComparison.OrdinalIgnoreCase)
                ? report
                : report.TrimEnd() + "\n\n" + Disclaimer;

        private static List<PlantDiseaseSuggestion> ToEntity(List<CropDiseaseSuggestion> diseases)
            => diseases.Take(5).Select(d => new PlantDiseaseSuggestion
            {
                Name = d.Name,
                ScientificName = d.ScientificName,
                Probability = d.Probability,
                Severity = d.Severity,
                Symptoms = d.Symptoms,
                Treatments = d.Treatments
            }).ToList();
    }
}
