using AgripeWebAPI.Models.Interfaces;

namespace AgripeWebWorker.Services
{
    // Stub used until the real AlertEmailService is provided by the Backend Lead issue.
    internal sealed class NoOpAlertEmailService : IAlertEmailService
    {
        private readonly ILogger<NoOpAlertEmailService> _logger;

        public NoOpAlertEmailService(ILogger<NoOpAlertEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendIrrigationAlertAsync(
            int pivotId,
            int userId,
            string? pivotName,
            decimal currentAverage,
            decimal projectedValue,
            decimal limiteInferior,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[AlertEmail] Irrigation alert for pivot {PivotId} ({PivotName}) user {UserId} — projected {Proj:F2} < limite {Limite} [email service not yet configured]",
                pivotId, pivotName ?? "?", userId, projectedValue, limiteInferior);
            return Task.CompletedTask;
        }
    }
}
