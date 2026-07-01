using AgripeWebAPI.Services.Forecast;

namespace AgripeWebAPI.Models.Interfaces
{
    public interface IAgricultureWeatherService
    {
        Task<IReadOnlyList<DailyAgricultureData>?> GetAgricultureDataAsync(
            double latitude, double longitude, int days, CancellationToken cancellationToken);
    }
}
