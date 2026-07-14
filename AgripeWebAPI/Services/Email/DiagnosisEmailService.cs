using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.Diagnosis;
using MongoDB.Driver;

namespace AgripeWebAPI.Services.Email
{
    public interface IDiagnosisEmailService
    {
        /// <summary>Manda ao produtor o laudo assinado, com o PDF anexado.</summary>
        Task SendSignedReportAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken);
    }

    public class DiagnosisEmailService : IDiagnosisEmailService
    {
        private readonly agpDBContext _dbContext;
        private readonly IEmailSender _sender;
        private readonly IDiagnosisPdfService _pdfService;
        private readonly IDiagnosisImageStore _imageStore;
        private readonly ILogger<DiagnosisEmailService> _logger;

        public DiagnosisEmailService(
            agpDBContext dbContext,
            IEmailSender sender,
            IDiagnosisPdfService pdfService,
            IDiagnosisImageStore imageStore,
            ILogger<DiagnosisEmailService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendSignedReportAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken)
        {
            var producer = await _dbContext.Users
                .Find(u => u.Id == diagnosis.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(producer?.Email))
            {
                _logger.LogWarning(
                    "[DiagnosisEmail] Produtor {UserId} sem e-mail; laudo {Id} não enviado.",
                    diagnosis.UserId, diagnosis.Id);
                return;
            }

            var image = await _imageStore.DownloadAsync(diagnosis.ImageFileId, cancellationToken);
            var pdf = _pdfService.Generate(diagnosis, producer.Name, image);

            var signature = diagnosis.Signature;
            var disease = diagnosis.ConfirmedDisease
                          ?? diagnosis.Diseases.FirstOrDefault()?.Name
                          ?? "resultado disponível";

            var body = EmailTemplates.Wrap(
                title: $"Laudo #{diagnosis.Id} assinado",
                content: $"""
                    <p>Seu laudo foi revisado e assinado por
                    <strong>{EmailTemplates.Escape(signature?.AgronomistName)}</strong>
                    {(string.IsNullOrWhiteSpace(signature?.Crea) ? "" : $"({EmailTemplates.Escape(signature.Crea)})")}.</p>

                    <p style="margin:16px 0;padding:12px;background:#F2F7F2;border:1px solid #D5E5D6;border-radius:8px">
                      <strong>Diagnóstico:</strong> {EmailTemplates.Escape(disease)}<br>
                      {(string.IsNullOrWhiteSpace(diagnosis.Prescription)
                        ? ""
                        : $"<strong>Prescrição:</strong> {EmailTemplates.Escape(diagnosis.Prescription)}")}
                    </p>

                    <p>O laudo completo está no PDF anexo.</p>

                    <p style="color:#5F6B60;font-size:12px;font-style:italic">
                      Laudo técnico informativo. Não constitui receituário agronômico nem ART.
                    </p>
                    """);

            await _sender.SendAsync(
                producer.Email,
                $"Laudo #{diagnosis.Id} assinado — {disease}",
                body,
                [new EmailAttachment($"laudo-{diagnosis.Id}.pdf", pdf, "application/pdf")],
                cancellationToken);
        }
    }
}
