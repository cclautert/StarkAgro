namespace AgripeWebAPI.Models.Interfaces
{
    public interface ILoRaWanDownlinkService
    {
        Task<bool> SendUplinkIntervalAsync(string devEui, int intervalSeconds, CancellationToken cancellationToken);
    }
}
