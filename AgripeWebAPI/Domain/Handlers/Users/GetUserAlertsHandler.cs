using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Users
{
    public class GetUserAlertsHandler : IRequestHandler<GetUserAlertsRequest, IList<UserAlertResponse>>
    {
        private const int WindowDays = 30;
        private const int MaxAlerts = 50;

        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetUserAlertsHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<IList<UserAlertResponse>> Handle(GetUserAlertsRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to list alerts.");

            var since = DateTime.UtcNow.AddDays(-WindowDays);

            var irrigationAlerts = await _dbContext.IrrigationAlerts
                .Find(x => x.UserId == userId && x.Date >= since)
                .ToListAsync(cancellationToken);

            var anomalies = await _dbContext.SensorAnomalies
                .Find(x => x.UserId == userId && x.Date >= since)
                .ToListAsync(cancellationToken);

            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);
            var readAt = user?.AlertsReadAt;

            var pivotIds = irrigationAlerts.Select(x => x.PivotId)
                .Concat(anomalies.Select(x => x.PivotId))
                .Distinct()
                .ToList();
            var pivots = pivotIds.Count == 0
                ? new List<Pivot>()
                : await _dbContext.Pivots.Find(x => pivotIds.Contains(x.Id)).ToListAsync(cancellationToken);
            var pivotNamesById = pivots.ToDictionary(x => x.Id, x => x.Name);

            var sensorIds = anomalies.Select(x => x.SensorId).Distinct().ToList();
            var sensors = sensorIds.Count == 0
                ? new List<Sensor>()
                : await _dbContext.Sensors.Find(x => sensorIds.Contains(x.Id)).ToListAsync(cancellationToken);
            var sensorsById = sensors.ToDictionary(x => x.Id, x => x);

            string PivotName(int pivotId) =>
                pivotNamesById.TryGetValue(pivotId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"Pivô {pivotId}";

            var alerts = irrigationAlerts.Select(a => new UserAlertResponse
                {
                    Id = $"irrigation-{a.Id}",
                    Title = $"Umidade projetada {a.ProjectedValue:0.#}% < limite {a.LimiteInferior:0.#}%",
                    PivotName = PivotName(a.PivotId),
                    AlertType = "MoistureLow",
                    CreatedAt = a.Date,
                    IsRead = readAt.HasValue && a.Date <= readAt.Value
                })
                .Concat(anomalies.Select(a => new UserAlertResponse
                {
                    Id = $"anomaly-{a.Id}",
                    Title = sensorsById.TryGetValue(a.SensorId, out var sensor)
                        ? $"Sensor {sensor.Code} — Quadrante {sensor.Quadrante}: {a.Value:0.#}% fora da faixa ({a.ExpectedMin:0.#}%–{a.ExpectedMax:0.#}%)"
                        : $"Leitura {a.Value:0.#}% fora da faixa ({a.ExpectedMin:0.#}%–{a.ExpectedMax:0.#}%)",
                    PivotName = PivotName(a.PivotId),
                    AlertType = "AnomalyPersisted",
                    CreatedAt = a.Date,
                    IsRead = readAt.HasValue && a.Date <= readAt.Value
                }))
                .OrderByDescending(x => x.CreatedAt)
                .Take(MaxAlerts)
                .ToList();

            return alerts;
        }
    }
}
