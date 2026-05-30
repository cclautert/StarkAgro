using AgripeWebAPI.Services.Forecast;

namespace AgripeWebAPI.Models.Interfaces
{
    public interface IAgricultureWeatherService
    {
        Task<AgricultureWeatherData?> GetAgricultureDataAsync(
            double latitude, double longitude, int days, CancellationToken cancellationToken);
    }
}
