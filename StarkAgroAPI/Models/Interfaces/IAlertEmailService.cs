namespace StarkAgroAPI.Models.Interfaces
{
    public interface IAlertEmailService
    {
        Task SendIrrigationAlertAsync(
            int pivotId,
            int userId,
            string? pivotName,
            decimal currentAverage,
            decimal projectedValue,
            decimal limiteInferior,
            CancellationToken cancellationToken = default);
    }
}
