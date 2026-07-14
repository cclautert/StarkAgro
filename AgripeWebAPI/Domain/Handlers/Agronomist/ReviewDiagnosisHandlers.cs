using AgripeWebAPI.Domain.Commands.Requests.Agronomist;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Services.Diagnosis;
using MediatR;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace AgripeWebAPI.Domain.Handlers.Agronomist
{
    /// <summary>Base dos handlers de escrita do agrônomo: carrega o laudo e aplica a regra de acesso.</summary>
    public abstract class AgronomistWriteHandlerBase
    {
        protected readonly agpDBContext DbContext;
        protected readonly ICurrentUserContext CurrentUser;
        protected readonly IDiagnosisAccessService AccessService;
        protected readonly INotifier Notifier;

        protected AgronomistWriteHandlerBase(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService,
            INotifier notifier)
        {
            DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            AccessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
            Notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        /// <summary>
        /// Devolve o laudo se o agrônomo autenticado pode escrever nele.
        /// O produtor dono <b>não</b> passa aqui: ele não revisa nem assina o próprio laudo.
        /// </summary>
        protected async Task<(int agronomistId, PlantDiagnosis? diagnosis)> LoadForWriteAsync(
            int diagnosisId,
            CancellationToken cancellationToken)
        {
            var agronomistId = CurrentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var diagnosis = await DbContext.PlantDiagnoses
                .Find(d => d.Id == diagnosisId)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null
                || diagnosis.AgronomistId != agronomistId
                || !await AccessService.HasActiveLinkAsync(agronomistId, diagnosis.UserId, cancellationToken))
            {
                Notifier.Handle(new Notification("Laudo não encontrado."));
                return (agronomistId, null);
            }

            return (agronomistId, diagnosis);
        }
    }

    public class ClaimDiagnosisHandler : AgronomistWriteHandlerBase, IRequestHandler<ClaimDiagnosisRequest, bool>
    {
        public ClaimDiagnosisHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService,
            INotifier notifier)
            : base(dbContext, currentUser, accessService, notifier) { }

        public async Task<bool> Handle(ClaimDiagnosisRequest request, CancellationToken cancellationToken)
        {
            var (agronomistId, diagnosis) = await LoadForWriteAsync(request.Id, cancellationToken);
            if (diagnosis is null) return false;

            if (diagnosis.Status != PlantDiagnosisStatus.PendingReview)
            {
                Notifier.Handle(new Notification("Este laudo não está aguardando revisão."));
                return false;
            }

            var now = DateTime.UtcNow;

            // Claim atômico: se outro agrônomo (ou outra aba) pegou primeiro, o filtro não casa.
            var result = await DbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id && d.Status == PlantDiagnosisStatus.PendingReview,
                Builders<PlantDiagnosis>.Update
                    .Set(d => d.Status, PlantDiagnosisStatus.InReview)
                    .Set(d => d.ReviewerId, agronomistId)
                    .Set(d => d.ReviewStartedAt, now)
                    .Set(d => d.UpdatedAt, now)
                    .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                    {
                        At = now,
                        ActorUserId = agronomistId,
                        FromStatus = PlantDiagnosisStatus.PendingReview,
                        ToStatus = PlantDiagnosisStatus.InReview,
                        Action = "claimed"
                    }),
                null,
                cancellationToken);

            if (result.ModifiedCount == 0)
            {
                Notifier.Handle(new Notification("Este laudo já foi assumido."));
                return false;
            }

            return true;
        }
    }

    /// <summary>Salva o rascunho da revisão. Não muda o status.</summary>
    public class ReviewDiagnosisHandler : AgronomistWriteHandlerBase, IRequestHandler<ReviewDiagnosisRequest, bool>
    {
        public ReviewDiagnosisHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService,
            INotifier notifier)
            : base(dbContext, currentUser, accessService, notifier) { }

        public async Task<bool> Handle(ReviewDiagnosisRequest request, CancellationToken cancellationToken)
        {
            var (agronomistId, diagnosis) = await LoadForWriteAsync(request.Id, cancellationToken);
            if (diagnosis is null) return false;

            if (diagnosis.Status != PlantDiagnosisStatus.InReview)
            {
                Notifier.Handle(new Notification("Assuma o laudo antes de editá-lo."));
                return false;
            }

            var now = DateTime.UtcNow;

            // AiReportMarkdown NÃO entra neste update: ele é imutável, e é o que permite
            // comparar depois o que a IA disse com o que o agrônomo assinou.
            await DbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id,
                Builders<PlantDiagnosis>.Update
                    .Set(d => d.AgronomistReportMarkdown, request.ReportMarkdown)
                    .Set(d => d.ConfirmedDisease, request.ConfirmedDisease)
                    .Set(d => d.AgronomistSeverity, request.Severity)
                    .Set(d => d.Prescription, request.Prescription)
                    .Set(d => d.UpdatedAt, now),
                null,
                cancellationToken);

            return true;
        }
    }

    public class SignDiagnosisHandler : AgronomistWriteHandlerBase, IRequestHandler<SignDiagnosisRequest, bool>
    {
        private readonly IPushNotificationService _pushService;
        private readonly Services.Email.IDiagnosisEmailService _emailService;
        private readonly ILogger<SignDiagnosisHandler> _logger;

        public SignDiagnosisHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService,
            INotifier notifier,
            IPushNotificationService pushService,
            Services.Email.IDiagnosisEmailService emailService,
            ILogger<SignDiagnosisHandler> logger)
            : base(dbContext, currentUser, accessService, notifier)
        {
            _pushService = pushService ?? throw new ArgumentNullException(nameof(pushService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> Handle(SignDiagnosisRequest request, CancellationToken cancellationToken)
        {
            var (agronomistId, diagnosis) = await LoadForWriteAsync(request.Id, cancellationToken);
            if (diagnosis is null) return false;

            // Signed é terminal: um laudo assinado é um ato profissional, não se assina duas vezes.
            if (diagnosis.Status is not (PlantDiagnosisStatus.InReview or PlantDiagnosisStatus.PendingReview))
            {
                Notifier.Handle(new Notification(
                    diagnosis.Status == PlantDiagnosisStatus.Signed
                        ? "Este laudo já foi assinado."
                        : "Este laudo não pode ser assinado no estado atual."));
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.ReportMarkdown))
            {
                Notifier.Handle(new Notification("O laudo não pode ser assinado em branco."));
                return false;
            }

            var agronomist = await DbContext.Users
                .Find(u => u.Id == agronomistId)
                .FirstOrDefaultAsync(cancellationToken);

            var crea = string.IsNullOrWhiteSpace(request.Crea) ? agronomist?.AgronomistCrea : request.Crea;
            var now = DateTime.UtcNow;

            var signature = new PlantDiagnosisSignature
            {
                AgronomistId = agronomistId,
                AgronomistName = agronomist?.Name ?? string.Empty,
                Crea = crea,
                SignedAt = now,
                ContentSha256 = Sha256(request.ReportMarkdown)
            };

            var result = await DbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id && d.Status != PlantDiagnosisStatus.Signed,
                Builders<PlantDiagnosis>.Update
                    .Set(d => d.Status, PlantDiagnosisStatus.Signed)
                    .Set(d => d.AgronomistReportMarkdown, request.ReportMarkdown)
                    .Set(d => d.ConfirmedDisease, request.ConfirmedDisease)
                    .Set(d => d.AgronomistSeverity, request.Severity)
                    .Set(d => d.Prescription, request.Prescription)
                    .Set(d => d.ReviewerId, agronomistId)
                    .Set(d => d.Signature, signature)
                    .Set(d => d.UpdatedAt, now)
                    .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                    {
                        At = now,
                        ActorUserId = agronomistId,
                        FromStatus = diagnosis.Status,
                        ToStatus = PlantDiagnosisStatus.Signed,
                        Action = "signed"
                    }),
                null,
                cancellationToken);

            if (result.ModifiedCount == 0)
            {
                Notifier.Handle(new Notification("Este laudo já foi assinado."));
                return false;
            }

            await NotifyProducerAsync(
                diagnosis.UserId,
                "Laudo assinado",
                $"Seu laudo foi revisado e assinado por {signature.AgronomistName}.",
                cancellationToken);

            // O e-mail leva o PDF assinado — é o documento que o produtor guarda. Como o laudo
            // já está gravado, uma falha aqui não pode desfazer a assinatura.
            try
            {
                diagnosis.Status = PlantDiagnosisStatus.Signed;
                diagnosis.AgronomistReportMarkdown = request.ReportMarkdown;
                diagnosis.ConfirmedDisease = request.ConfirmedDisease;
                diagnosis.AgronomistSeverity = request.Severity;
                diagnosis.Prescription = request.Prescription;
                diagnosis.Signature = signature;

                await _emailService.SendSignedReportAsync(diagnosis, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao enviar o e-mail do laudo {Id}; a assinatura foi mantida.", diagnosis.Id);
            }

            return true;
        }

        private async Task NotifyProducerAsync(int userId, string title, string body, CancellationToken ct)
        {
            try
            {
                await _pushService.SendAsync(userId, title, body, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Push é acessório: o laudo já está assinado e aparece na tela do produtor.
                _ = ex;
            }
        }

        private static string Sha256(string content)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    public class RejectDiagnosisHandler : AgronomistWriteHandlerBase, IRequestHandler<RejectDiagnosisRequest, bool>
    {
        private readonly IPushNotificationService _pushService;

        public RejectDiagnosisHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisAccessService accessService,
            INotifier notifier,
            IPushNotificationService pushService)
            : base(dbContext, currentUser, accessService, notifier)
        {
            _pushService = pushService ?? throw new ArgumentNullException(nameof(pushService));
        }

        public async Task<bool> Handle(RejectDiagnosisRequest request, CancellationToken cancellationToken)
        {
            var (agronomistId, diagnosis) = await LoadForWriteAsync(request.Id, cancellationToken);
            if (diagnosis is null) return false;

            if (PlantDiagnosisStatus.IsTerminal(diagnosis.Status))
            {
                Notifier.Handle(new Notification("Este laudo não pode mais ser alterado."));
                return false;
            }

            var now = DateTime.UtcNow;

            await DbContext.PlantDiagnoses.UpdateOneAsync(
                d => d.Id == diagnosis.Id,
                Builders<PlantDiagnosis>.Update
                    .Set(d => d.Status, PlantDiagnosisStatus.Rejected)
                    .Set(d => d.RejectionReason, request.Reason)
                    .Set(d => d.ReviewerId, agronomistId)
                    .Set(d => d.UpdatedAt, now)
                    .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                    {
                        At = now,
                        ActorUserId = agronomistId,
                        FromStatus = diagnosis.Status,
                        ToStatus = PlantDiagnosisStatus.Rejected,
                        Action = "rejected:agronomist"
                    }),
                null,
                cancellationToken);

            try
            {
                await _pushService.SendAsync(
                    diagnosis.UserId,
                    "Laudo devolvido pelo agrônomo",
                    request.Reason,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _ = ex;
            }

            return true;
        }
    }
}
