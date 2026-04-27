using AgripeWebAPI.Models;

namespace AgripeWebAPI.Models.Interfaces
{
    public interface IWeatherForecastService
    {
        Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, int days, CancellationToken cancellationToken);
    }
}
