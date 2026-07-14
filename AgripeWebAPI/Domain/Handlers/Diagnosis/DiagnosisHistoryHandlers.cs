using AgripeWebAPI.Domain.Commands.Requests.Diagnosis;
using AgripeWebAPI.Domain.Commands.Responses.Diagnosis;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Services.Diagnosis;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Diagnosis
{
    /// <summary>
    /// PDF do laudo. Serve tanto o produtor quanto o agrônomo vinculado — a regra de acesso
    /// é a mesma, então o handler é um só.
    /// </summary>
    public class GetDiagnosisPdfHandler : IRequestHandler<GetDiagnosisPdfRequest, DiagnosisPdfResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisAccessService _accessService;
        private readonly IDiagnosisImageStore _imageStore;
        private readonly IDiagnosisPdfService _pdfService;

        public GetDiagnosisPdfHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService,
            IDiagnosisImageStore imageStore,
            IDiagnosisPdfService pdfService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
            _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
        }

        public async Task<DiagnosisPdfResponse?> Handle(
            GetDiagnosisPdfRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null) return null;
            if (!await _accessService.CanAccessAsync(userId, diagnosis, cancellationToken)) return null;

            // Um laudo sem análise nenhuma não vira documento.
            if (string.IsNullOrWhiteSpace(diagnosis.AgronomistReportMarkdown)
                && string.IsNullOrWhiteSpace(diagnosis.AiReportMarkdown))
            {
                return null;
            }

            var producer = await _dbContext.Users
                .Find(u => u.Id == diagnosis.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            var image = await _imageStore.DownloadAsync(diagnosis.ImageFileId, cancellationToken);

            var content = _pdfService.Generate(diagnosis, producer?.Name, image);

            return new DiagnosisPdfResponse
            {
                Content = content,
                FileName = $"laudo-{diagnosis.Id}.pdf"
            };
        }
    }

    /// <summary>
    /// Reenfileira um laudo que falhou. O produtor não precisa tirar a foto de novo —
    /// a imagem continua no GridFS.
    /// </summary>
    public class ReprocessDiagnosisHandler : IRequestHandler<ReprocessDiagnosisRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public ReprocessDiagnosisHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<bool> Handle(ReprocessDiagnosisRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id && d.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null)
            {
                _notifier.Handle(new Notification("Laudo não encontrado."));
                return false;
            }

            // Só reprocessa o que falhou por problema nosso. Rejected é veredito sobre a foto
            // (ruim, ou não é planta): reprocessar daria o mesmo resultado e cobraria de novo.
            if (diagnosis.Status != PlantDiagnosisStatus.Failed)
            {
                _notifier.Handle(new Notification(
                    diagnosis.Status == PlantDiagnosisStatus.Rejected
                        ? "Esta foto foi recusada na análise. Envie uma nova foto."
                        : "Este laudo não está em estado de falha."));
                return false;
            }

            var now = DateTime.UtcNow;

            await _dbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id && d.UserId == userId,
                Builders<PlantDiagnosis>.Update
                    .Set(d => d.Status, PlantDiagnosisStatus.Uploaded)
                    .Set(d => d.RetryCount, 0)
                    .Set(d => d.NextAttemptAt, now)
                    .Set(d => d.FailureReason, (string?)null)
                    .Set(d => d.UpdatedAt, now)
                    .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                    {
                        At = now,
                        ActorUserId = userId,
                        FromStatus = PlantDiagnosisStatus.Failed,
                        ToStatus = PlantDiagnosisStatus.Uploaded,
                        Action = "reprocess-requested"
                    }),
                null,
                cancellationToken);

            return true;
        }
    }

    /// <summary>
    /// Histórico do talhão: responde "a mancha piorou desde a última vez?" — a pergunta que
    /// um app de foto avulsa nunca consegue responder.
    /// </summary>
    public class GetDiagnosisHistoryHandler
        : IRequestHandler<GetDiagnosisHistoryRequest, DiagnosisHistoryResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetDiagnosisHistoryHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<DiagnosisHistoryResponse> Handle(
            GetDiagnosisHistoryRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            // Só entra na linha do tempo o laudo que JÁ TEM análise. Um laudo ainda na fila
            // (Uploaded/Processing) não tem doença nem probabilidade — incluí-lo o colocaria
            // como o ponto mais recente e anularia a comparação de evolução.
            var analysed = new[]
            {
                PlantDiagnosisStatus.AiCompleted,
                PlantDiagnosisStatus.PendingReview,
                PlantDiagnosisStatus.InReview,
                PlantDiagnosisStatus.Signed
            };

            var diagnoses = await _dbContext.PlantDiagnoses
                .Find(d => d.UserId == userId
                           && d.PivotId == request.PivotId
                           && analysed.Contains(d.Status))
                .SortBy(d => d.CapturedAt)
                .ToListAsync(cancellationToken);

            var response = new DiagnosisHistoryResponse
            {
                PivotId = request.PivotId,
                PivotName = diagnoses.LastOrDefault()?.ContextSnapshot?.PivotName,
                Items = diagnoses.Select(d => new DiagnosisHistoryItemResponse
                {
                    Id = d.Id,
                    Status = d.Status,
                    CapturedAt = d.CapturedAt,
                    TopDisease = d.Diseases.FirstOrDefault()?.Name,
                    TopProbability = d.TopProbability,
                    ConfirmedDisease = d.ConfirmedDisease,
                    Severity = d.AgronomistSeverity,
                    MoistureAvg7d = d.ContextSnapshot?.MoistureAvg7d,
                    DaysAboveUpperLimit = d.ContextSnapshot?.DaysAboveUpperLimit ?? 0,
                    IsSigned = d.Signature is not null,
                    ImageUrl = $"/api/v1/diagnosis/{d.Id}/image"
                }).ToList()
            };

            response.Trend = BuildTrend(response.Items);
            return response;
        }

        /// <summary>
        /// Compara o primeiro e o último laudo do talhão. Só afirma piora/melhora quando a
        /// doença é a mesma — comparar a probabilidade de duas doenças diferentes não diz nada.
        /// </summary>
        private static string? BuildTrend(List<DiagnosisHistoryItemResponse> items)
        {
            if (items.Count < 2) return null;

            var first = items[0];
            var last = items[^1];

            // Comparar `ConfirmedDisease ?? TopDisease` misturava eixos: o laudo antigo traria o
            // nome comum sugerido pelo classificador ("Pinta-preta") e o novo, o nome científico
            // confirmado pelo agrônomo ("Alternaria solani") — a MESMA doença, reportada como
            // mudança de diagnóstico. Só se compara confirmado com confirmado, ou sugerido com
            // sugerido.
            var bothConfirmed = first.ConfirmedDisease is not null && last.ConfirmedDisease is not null;

            var firstDisease = bothConfirmed ? first.ConfirmedDisease : first.TopDisease;
            var lastDisease = bothConfirmed ? last.ConfirmedDisease : last.TopDisease;

            var days = (int)Math.Round((last.CapturedAt - first.CapturedAt).TotalDays);
            var since = $"desde {first.CapturedAt:dd/MM}";

            if (firstDisease is null || lastDisease is null) return null;

            if (!string.Equals(firstDisease, lastDisease, StringComparison.OrdinalIgnoreCase))
            {
                return $"O diagnóstico mudou {since}: de {firstDisease} para {lastDisease} " +
                       $"({days} dia(s), {items.Count} laudos).";
            }

            var delta = last.TopProbability - first.TopProbability;

            var from = ProbabilityFormatter.ToPercent(first.TopProbability);
            var to = ProbabilityFormatter.ToPercent(last.TopProbability);

            if (delta >= 0.10)
                return $"{lastDisease} piorou {since}: a confiança do diagnóstico subiu de " +
                       $"{from} para {to} em {days} dia(s).";

            if (delta <= -0.10)
                return $"{lastDisease} recuou {since}: a confiança do diagnóstico caiu de " +
                       $"{from} para {to} em {days} dia(s).";

            return $"{lastDisease} permanece estável {since} ({items.Count} laudos em {days} dia(s)).";
        }
    }

    public class GetDiagnosisQuotaHandler : IRequestHandler<GetDiagnosisQuotaRequest, DiagnosisQuotaResponse>
    {
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisQuotaService _quotaService;

        public GetDiagnosisQuotaHandler(ICurrentUserContext currentUser, IDiagnosisQuotaService quotaService)
        {
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        }

        public async Task<DiagnosisQuotaResponse> Handle(
            GetDiagnosisQuotaRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var quota = await _quotaService.GetAsync(userId, cancellationToken);

            return new DiagnosisQuotaResponse
            {
                Limit = quota.Limit,
                Used = quota.Used,
                Remaining = quota.IsUnlimited ? 0 : quota.Remaining,
                IsUnlimited = quota.IsUnlimited,
                IsExhausted = quota.IsExhausted,
                ResetsAt = quota.ResetsAt
            };
        }
    }

    public class GetDiagnosisAuditHandler
        : IRequestHandler<GetDiagnosisAuditRequest, List<DiagnosisAuditEntryResponse>?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisAccessService _accessService;

        public GetDiagnosisAuditHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
        }

        public async Task<List<DiagnosisAuditEntryResponse>?> Handle(
            GetDiagnosisAuditRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null) return null;
            if (!await _accessService.CanAccessAsync(userId, diagnosis, cancellationToken)) return null;

            var actorIds = diagnosis.AuditTrail
                .Where(a => a.ActorUserId.HasValue)
                .Select(a => a.ActorUserId!.Value)
                .Distinct()
                .ToList();

            var actors = actorIds.Count == 0
                ? []
                : await _dbContext.Users.Find(u => actorIds.Contains(u.Id)).ToListAsync(cancellationToken);

            var namesById = actors.ToDictionary(u => u.Id, u => u.Name);

            return diagnosis.AuditTrail
                .OrderBy(a => a.At)
                .Select(a => new DiagnosisAuditEntryResponse
                {
                    At = a.At,
                    ActorUserId = a.ActorUserId,
                    ActorName = a.ActorUserId.HasValue ? namesById.GetValueOrDefault(a.ActorUserId.Value) : "sistema",
                    FromStatus = a.FromStatus,
                    ToStatus = a.ToStatus,
                    Action = a.Action
                })
                .ToList();
        }
    }
}
