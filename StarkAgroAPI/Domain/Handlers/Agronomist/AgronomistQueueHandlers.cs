using StarkAgroAPI.Domain.Commands.Requests.Agronomist;
using StarkAgroAPI.Domain.Commands.Responses.Agronomist;
using StarkAgroAPI.Domain.Commands.Responses.Diagnosis;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Diagnosis;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Agronomist
{
    /// <summary>
    /// Fila do agrônomo: os laudos dos clientes com vínculo <b>ativo</b>.
    /// <para>
    /// O filtro combina o <c>AgronomistId</c> denormalizado no laudo com a lista de clientes
    /// ativos — então revogar um vínculo faz os laudos daquele produtor sumirem da fila
    /// imediatamente, sem backfill.
    /// </para>
    /// </summary>
    public class GetAgronomistQueueHandler
        : IRequestHandler<GetAgronomistQueueRequest, List<AgronomistQueueItemResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisAccessService _accessService;

        public GetAgronomistQueueHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
        }

        public async Task<List<AgronomistQueueItemResponse>> Handle(
            GetAgronomistQueueRequest request,
            CancellationToken cancellationToken)
        {
            var agronomistId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var clientIds = await _accessService.GetActiveClientIdsAsync(agronomistId, cancellationToken);
            if (clientIds.Count == 0) return [];

            var filter = Builders<PlantDiagnosis>.Filter.And(
                Builders<PlantDiagnosis>.Filter.Eq(d => d.AgronomistId, agronomistId),
                Builders<PlantDiagnosis>.Filter.In(d => d.UserId, clientIds));

            filter &= string.IsNullOrWhiteSpace(request.Status)
                ? Builders<PlantDiagnosis>.Filter.In(d => d.Status,
                    new[] { PlantDiagnosisStatus.PendingReview, PlantDiagnosisStatus.InReview })
                : Builders<PlantDiagnosis>.Filter.Eq(d => d.Status, request.Status);

            var pageSize = request.PageSize is > 0 and <= 100 ? request.PageSize : 20;
            var pageIndex = request.PageIndex > 0 ? request.PageIndex : 0;

            var diagnoses = await _dbContext.PlantDiagnoses
                .Find(filter)
                .SortByDescending(d => d.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            if (diagnoses.Count == 0) return [];

            var userIds = diagnoses.Select(d => d.UserId).Distinct().ToList();
            var clients = await _dbContext.Users
                .Find(u => userIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            var namesById = clients.ToDictionary(u => u.Id, u => u.Name);

            return diagnoses.Select(d => new AgronomistQueueItemResponse
            {
                Id = d.Id,
                Status = d.Status,
                ClientUserId = d.UserId,
                ClientName = namesById.GetValueOrDefault(d.UserId),
                PivotName = d.ContextSnapshot?.PivotName,
                CropName = d.CropName,
                TopDisease = d.Diseases.FirstOrDefault()?.Name,
                TopProbability = d.TopProbability,
                CreatedAt = d.CreatedAt,
                ReviewStartedAt = d.ReviewStartedAt,
                ImageUrl = $"/api/v1/agronomist/diagnosis/{d.Id}/image"
            }).ToList();
        }
    }

    /// <summary>Detalhe do laudo para o agrônomo — passa pela mesma regra de acesso.</summary>
    public class GetAgronomistDiagnosisHandler
        : IRequestHandler<GetAgronomistDiagnosisRequest, PlantDiagnosisResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisAccessService _accessService;

        public GetAgronomistDiagnosisHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
        }

        public async Task<PlantDiagnosisResponse?> Handle(
            GetAgronomistDiagnosisRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null) return null;
            if (!await _accessService.CanAccessAsync(userId, diagnosis, cancellationToken)) return null;

            var client = await _dbContext.Users
                .Find(u => u.Id == diagnosis.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            return DiagnosisResponseMapper.ToResponse(diagnosis, client?.Name, forAgronomist: true);
        }
    }

    /// <summary>Imagem do laudo para o agrônomo — o endpoint que mais costuma ficar sem proteção.</summary>
    public class GetAgronomistDiagnosisImageHandler
        : IRequestHandler<GetAgronomistDiagnosisImageRequest, PlantDiagnosisImageResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisAccessService _accessService;
        private readonly IDiagnosisImageStore _imageStore;

        public GetAgronomistDiagnosisImageHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService,
            IDiagnosisImageStore imageStore)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
        }

        public async Task<PlantDiagnosisImageResponse?> Handle(
            GetAgronomistDiagnosisImageRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null) return null;
            if (!await _accessService.CanAccessAsync(userId, diagnosis, cancellationToken)) return null;

            var content = await _imageStore.DownloadAsync(diagnosis.ImageFileId, cancellationToken);
            if (content is null) return null;

            return new PlantDiagnosisImageResponse
            {
                Content = content,
                ContentType = diagnosis.ImageContentType
            };
        }
    }

    /// <summary>Monta o DTO de detalhe — compartilhado entre a visão do produtor e a do agrônomo.</summary>
    public static class DiagnosisResponseMapper
    {
        public static PlantDiagnosisResponse ToResponse(
            PlantDiagnosis d,
            string? clientName = null,
            bool forAgronomist = false)
        {
            var basePath = forAgronomist ? "agronomist/diagnosis" : "diagnosis";

            return new PlantDiagnosisResponse
            {
                Id = d.Id,
                Status = d.Status,
                PivotId = d.PivotId,
                CropName = d.CropName,
                ProducerNotes = d.ProducerNotes,
                Latitude = d.Latitude,
                Longitude = d.Longitude,
                CapturedAt = d.CapturedAt,
                CreatedAt = d.CreatedAt,
                ProcessedAt = d.ProcessedAt,
                IsPlant = d.IsPlant,
                TopProbability = d.TopProbability,
                ClientName = clientName,
                Diseases = d.Diseases.Select(x => new DiseaseSuggestionResponse
                {
                    Name = x.Name,
                    ScientificName = x.ScientificName,
                    Probability = x.Probability,
                    Severity = x.Severity,
                    Symptoms = x.Symptoms,
                    Treatments = x.Treatments
                }).ToList(),
                Context = d.ContextSnapshot is null ? null : new DiagnosisContextResponse
                {
                    PivotName = d.ContextSnapshot.PivotName,
                    MoistureAvg7d = d.ContextSnapshot.MoistureAvg7d,
                    LimiteInferior = d.ContextSnapshot.LimiteInferior,
                    LimiteSuperior = d.ContextSnapshot.LimiteSuperior,
                    DaysAboveUpperLimit = d.ContextSnapshot.DaysAboveUpperLimit,
                    OpenAnomalies = d.ContextSnapshot.OpenAnomalies,
                    IrrigationAlerts7d = d.ContextSnapshot.IrrigationAlerts7d,
                    ForecastSummary = d.ContextSnapshot.ForecastSummary
                },
                AiReportMarkdown = d.AiReportMarkdown,
                AiProvider = d.AiProvider,
                AgronomistReportMarkdown = d.AgronomistReportMarkdown,
                ConfirmedDisease = d.ConfirmedDisease,
                AgronomistSeverity = d.AgronomistSeverity,
                Prescription = d.Prescription,
                RejectionReason = d.RejectionReason,
                Signature = d.Signature is null ? null : new DiagnosisSignatureResponse
                {
                    AgronomistName = d.Signature.AgronomistName,
                    Crea = d.Signature.Crea,
                    SignedAt = d.Signature.SignedAt,
                    ContentSha256 = d.Signature.ContentSha256
                },
                FailureReason = d.FailureReason,
                ImageUrl = $"/api/v1/{basePath}/{d.Id}/image"
            };
        }
    }
}
