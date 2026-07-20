using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Services
{
    public interface ISensorAnomalyService
    {
        Task<bool> DetectAndSaveAsync(
            ReadSensor reading,
            int pivotId,
            IReadOnlyList<ReadSensor> lastNReadings,
            CancellationToken cancellationToken = default);
    }
}
