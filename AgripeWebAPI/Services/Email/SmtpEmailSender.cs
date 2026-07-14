using AgripeWebAPI.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AgripeWebAPI.Services.Email
{
    public record EmailAttachment(string FileName, byte[] Content, string ContentType);

    public interface IEmailSender
    {
        /// <summary>Devolve <c>false</c> quando o SMTP não está configurado ou o envio falha.</summary>
        Task<bool> SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            IEnumerable<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Envio real por SMTP. Sem configuração, registra em log e devolve <c>false</c> — nunca
    /// derruba o fluxo: um alerta ou um laudo não pode falhar porque o e-mail caiu.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            IEnumerable<EmailAttachment>? attachments = null,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.IsConfigured)
            {
                _logger.LogInformation(
                    "[Email] SMTP não configurado — e-mail para {To} ({Subject}) não foi enviado.",
                    toEmail, subject);
                return false;
            }

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("[Email] Destinatário vazio; nada a enviar.");
                return false;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };

            foreach (var attachment in attachments ?? [])
            {
                builder.Attachments.Add(
                    attachment.FileName,
                    attachment.Content,
                    ContentType.Parse(attachment.ContentType));
            }

            message.Body = builder.ToMessageBody();

            try
            {
                using var client = new SmtpClient();

                await client.ConnectAsync(
                    _settings.Host,
                    _settings.Port,
                    _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(_settings.Username))
                {
                    await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("[Email] Enviado para {To}: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[Email] Falha ao enviar para {To}: {Subject}", toEmail, subject);
                return false;
            }
        }
    }
}
