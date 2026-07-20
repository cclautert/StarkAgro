using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using StarkAgroAPI.Configuration;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.AIInsights;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Text;

namespace StarkAgroAPI.Domain.Handlers.Pivots
{
    public class GetPivotAIInsightsHandler : IRequestHandler<GetPivotAIInsightsRequest, PivotAIInsightsResponse?>
    {
        private const int ReadingsPerSensor = 48;
        private const int MaxSensors = 6;
        private const int RecentAnomalies = 10;

        private readonly agpDBContext _dbContext;
        private readonly IAIInsightsServiceFactory _serviceFactory;
        private readonly IWeatherForecastService _forecastService;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;
        private readonly IMemoryCache _cache;
        private readonly AISettings _aiSettings;
        private readonly ILogger<GetPivotAIInsightsHandler> _logger;

        public GetPivotAIInsightsHandler(
            agpDBContext dbContext,
            IAIInsightsServiceFactory serviceFactory,
            IWeatherForecastService forecastService,
            ICurrentUserContext currentUser,
            INotifier notifier,
            IMemoryCache cache,
            IOptions<AISettings> aiSettings,
            ILogger<GetPivotAIInsightsHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _serviceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _aiSettings = aiSettings?.Value ?? throw new ArgumentNullException(nameof(aiSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PivotAIInsightsResponse?> Handle(GetPivotAIInsightsRequest request, CancellationToken cancellationToken)
        {
            if (request.PivotId <= 0)
            {
                _notifier.Handle(new Notification("PivotId inválido."));
                return null;
            }

            var cacheKey = $"ai-insights:{request.PivotId}";
            if (_cache.TryGetValue<PivotAIInsightsResponse>(cacheKey, out var cached) && cached is not null)
            {
                return new PivotAIInsightsResponse
                {
                    Insights = cached.Insights,
                    GeneratedAt = cached.GeneratedAt,
                    FromCache = true
                };
            }

            var pivot = await _dbContext.Pivots
                .Find(p => p.Id == request.PivotId && p.UserId == _currentUser.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pivot is null)
            {
                _notifier.Handle(new Notification("Pivot não encontrado."));
                return null;
            }

            var user = await _dbContext.Users
                .Find(u => u.Id == pivot.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            var aiSettings = await _dbContext.PlatformAiSettings
                .Find(_ => true)
                .FirstOrDefaultAsync(cancellationToken);

            if (aiSettings == null)
            {
                _notifier.Handle(new Notification("Chave da API de IA não configurada. Contate o administrador."));
                return null;
            }

            var aiService = _serviceFactory.GetService(aiSettings.ActiveProvider);
            if (aiService == null)
            {
                _notifier.Handle(new Notification($"Provider de IA '{aiSettings.ActiveProvider}' não é suportado."));
                return null;
            }

            var (apiKey, modelOverride) = aiSettings.ActiveProvider.ToLower() switch
            {
                "gemini"    => (aiSettings.GeminiKey,    aiSettings.GeminiModel),
                "anthropic" => (aiSettings.AnthropicKey, aiSettings.AnthropicModel),
                "openai"    => (aiSettings.OpenAiKey,    aiSettings.OpenAiModel),
                _           => ((string?)null, (string?)null)
            };

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _notifier.Handle(new Notification("Chave da API de IA não configurada. Contate o administrador."));
                return null;
            }

            var limiteInferior = pivot.LimiteInferior ?? user?.LimiteInferior ?? 25m;
            var limiteSuperior = pivot.LimiteSuperior ?? user?.LimiteSuperior ?? 75m;

            var sensors = await _dbContext.Sensors
                .Find(s => s.PivoId == pivot.Id && s.UserId == pivot.UserId)
                .Limit(MaxSensors)
                .ToListAsync(cancellationToken);

            var since = DateTime.UtcNow.AddHours(-48);
            var sensorReadings = new List<SensorReadingContext>();
            foreach (var sensor in sensors)
            {
                var readings = await _dbContext.ReadSensors
                    .Find(r => r.SensorId == sensor.Id && r.Date >= since)
                    .SortByDescending(r => r.Date)
                    .Limit(ReadingsPerSensor)
                    .ToListAsync(cancellationToken);

                sensorReadings.Add(new SensorReadingContext
                {
                    SensorCode = sensor.Code,
                    Quadrante = sensor.Quadrante,
                    Readings = readings.Select(r => new ReadingPoint { Value = r.Humidity ?? 0, Date = r.Date }).ToList()
                });
            }

            string? forecastSummary = null;
            if (pivot.Latitude.HasValue && pivot.Longitude.HasValue)
            {
                try
                {
                    var forecast = await _forecastService.GetForecastAsync(
                        pivot.Latitude.Value, pivot.Longitude.Value, 7, cancellationToken);

                    if (forecast.IsAvailable)
                        forecastSummary = BuildForecastSummary(forecast);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not fetch forecast for pivot {PivotId}", pivot.Id);
                }
            }

            var recentAnomalies = new List<AnomalyContext>();
            var sensorIds = sensors.Select(s => s.Id).ToList();
            if (sensorIds.Count > 0)
            {
                var anomalyFilter = Builders<SensorAnomaly>.Filter.And(
                    Builders<SensorAnomaly>.Filter.In(a => a.SensorId, sensorIds),
                    Builders<SensorAnomaly>.Filter.Eq(a => a.UserId, pivot.UserId),
                    Builders<SensorAnomaly>.Filter.Eq(a => a.Acknowledged, false));

                var anomalies = await _dbContext.SensorAnomalies
                    .Find(anomalyFilter)
                    .SortByDescending(a => a.Date)
                    .Limit(RecentAnomalies)
                    .ToListAsync(cancellationToken);

                recentAnomalies = anomalies.Select(a => new AnomalyContext
                {
                    SensorId = a.SensorId,
                    Value = a.Value,
                    ExpectedMin = a.ExpectedMin,
                    ExpectedMax = a.ExpectedMax,
                    Date = a.Date
                }).ToList();
            }

            var context = new PivotAIContext
            {
                PivotName = pivot.Name ?? pivot.Id.ToString(),
                LimiteInferior = limiteInferior,
                LimiteSuperior = limiteSuperior,
                Latitude = pivot.Latitude,
                Longitude = pivot.Longitude,
                SensorReadings = sensorReadings,
                ForecastSummary = forecastSummary,
                RecentAnomalies = recentAnomalies,
                ApiKeyOverride = apiKey,
                ModelOverride  = modelOverride
            };

            var insights = await aiService.GetInsightsAsync(context, cancellationToken);
            if (string.IsNullOrWhiteSpace(insights))
            {
                _notifier.Handle(new Notification("Assistente IA indisponível. Tente novamente em alguns minutos."));
                return null;
            }

            var response = new PivotAIInsightsResponse
            {
                Insights = insights,
                GeneratedAt = DateTime.UtcNow,
                FromCache = false
            };

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(_aiSettings.CacheDurationMinutes));

            _logger.LogInformation("AI insights generated for pivot {PivotId} (user {UserId})", pivot.Id, pivot.UserId);
            return response;
        }

        private static string BuildForecastSummary(WeatherForecast forecast)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Precipitação total prevista: {forecast.TotalPrecipitationMm:0.0} mm (fonte: {forecast.Source})");
            foreach (var day in forecast.DailyForecasts)
            {
                var prob = day.ProbabilityPercent.HasValue
                    ? $" ({day.ProbabilityPercent.Value:0}% prob.)"
                    : string.Empty;
                sb.AppendLine($"  {day.Date:dd/MM}: {day.PrecipitationMm:0.0} mm{prob}");
            }
            return sb.ToString();
        }
    }
}
