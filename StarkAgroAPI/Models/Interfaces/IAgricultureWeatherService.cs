using StarkAgroAPI.Services.Forecast;

namespace StarkAgroAPI.Models.Interfaces
{
    public interface IAgricultureWeatherService
    {
        Task<IReadOnlyList<DailyAgricultureData>?> GetAgricultureDataAsync(
            double latitude, double longitude, int days, CancellationToken cancellationToken);

        /// <summary>
        /// Accumulated precipitation (mm) observed over the last <paramref name="pastDays"/> days
        /// plus today, at the given location. Returns null when the data is unavailable.
        /// </summary>
        Task<double?> GetRecentPrecipitationAsync(
            double latitude, double longitude, int pastDays, CancellationToken cancellationToken);
    }
}
