using AgripeWebAPI.Configuration;
using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class GetPivotForecastHandler : IRequestHandler<GetPivotForecastRequest, GetPivotForecastResponse?>
    {
        private const int MinForecastDays = 1;
        private const int MaxForecastDays = 14;

        private readonly agpDBContext _dbContext;
        private readonly IWeatherForecastService _forecastService;
        private readonly INotifier _notifier;
        private readonly WeatherForecastSettings _settings;

        public GetPivotForecastHandler(
            agpDBContext dbContext,
            IWeatherForecastService forecastService,
            INotifier notifier,
            IOptions<WeatherForecastSettings> settings)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<GetPivotForecastResponse?> Handle(GetPivotForecastRequest request, CancellationToken cancellationToken)
        {
            if (request.PivotId is null || request.PivotId <= 0)
            {
                _notifier.Handle(new Notification("PivotId is required."));
                return null;
            }

            var requestedDays = request.Days ?? _settings.PivotDashboardForecastDays;
            if (requestedDays < MinForecastDays || requestedDays > MaxForecastDays)
            {
                _notifier.Handle(new Notification($"Days must be between {MinForecastDays} and {MaxForecastDays}."));
                return null;
            }

            var pivot = await _dbContext.Pivots
                .Find(p => p.Id == request.PivotId && p.UserId == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pivot is null)
            {
                _notifier.Handle(new Notification("Pivot not found."));
                return null;
            }

            var response = new GetPivotForecastResponse
            {
                PivotId = pivot.Id,
                PivotName = pivot.Name,
                Latitude = pivot.Latitude,
                Longitude = pivot.Longitude,
                Days = requestedDays,
                HasCoordinates = pivot.Latitude.HasValue && pivot.Longitude.HasValue,
                Forecast = null,
                Message = null
            };

            if (!response.HasCoordinates)
            {
                response.Message = "Pivô sem coordenadas. Cadastre a localização para visualizar a previsão do tempo.";
                return response;
            }

            var forecast = await _forecastService.GetForecastAsync(
                pivot.Latitude!.Value,
                pivot.Longitude!.Value,
                requestedDays,
                cancellationToken);

            response.Forecast = forecast;
            if (!forecast.IsAvailable)
            {
                response.Message = "Previsão do tempo indisponível no momento. Tente novamente em alguns minutos.";
            }

            return response;
        }
    }
}
