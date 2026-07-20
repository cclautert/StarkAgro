using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Email
{
    /// <summary>
    /// Alerta de irrigação por e-mail. Substitui o <c>NoOpAlertEmailService</c>, que só logava:
    /// até agora o StarkAgro <b>não enviava e-mail nenhum</b>.
    /// </summary>
    public class AlertEmailService : IAlertEmailService
    {
        private readonly agpDBContext _dbContext;
        private readonly IEmailSender _sender;
        private readonly ILogger<AlertEmailService> _logger;

        public AlertEmailService(
            agpDBContext dbContext,
            IEmailSender sender,
            ILogger<AlertEmailService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendIrrigationAlertAsync(
            int pivotId,
            int userId,
            string? pivotName,
            decimal currentAverage,
            decimal projectedValue,
            decimal limiteInferior,
            CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(user?.Email))
            {
                _logger.LogWarning("[AlertEmail] Usuário {UserId} sem e-mail; alerta não enviado.", userId);
                return;
            }

            var name = pivotName ?? $"Pivô {pivotId}";

            var body = EmailTemplates.Wrap(
                title: "Alerta de irrigação",
                content: $"""
                    <p>O <strong>{EmailTemplates.Escape(name)}</strong> deve ficar abaixo do limite de umidade
                    nas próximas horas.</p>
                    <table role="presentation" style="border-collapse:collapse;margin:16px 0">
                      <tr>
                        <td style="padding:6px 16px 6px 0;color:#5F6B60">Umidade atual (média)</td>
                        <td style="padding:6px 0;font-weight:600">{currentAverage:0.0}%</td>
                      </tr>
                      <tr>
                        <td style="padding:6px 16px 6px 0;color:#5F6B60">Projeção</td>
                        <td style="padding:6px 0;font-weight:600;color:#B3261E">{projectedValue:0.0}%</td>
                      </tr>
                      <tr>
                        <td style="padding:6px 16px 6px 0;color:#5F6B60">Limite configurado</td>
                        <td style="padding:6px 0;font-weight:600">{limiteInferior:0.0}%</td>
                      </tr>
                    </table>
                    <p>Considere irrigar antes que a umidade caia abaixo do limite.</p>
                    """);

            await _sender.SendAsync(
                user.Email,
                $"Alerta de irrigação — {name}",
                body,
                cancellationToken: cancellationToken);
        }
    }
}
