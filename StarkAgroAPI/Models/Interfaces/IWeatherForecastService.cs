using StarkAgroAPI.Models;

namespace StarkAgroAPI.Models.Interfaces
{
    public interface IWeatherForecastService
    {
        Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, int days, CancellationToken cancellationToken);
    }
}
