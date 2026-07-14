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

            // Convite de agrônomo pendente. Sem isto ele só aparecia na tela de Laudos — e o
            // produtor não tem motivo nenhum para abrir aquela tela sabendo que foi convidado.
            var invites = await GetPendingInvitesAsync(userId, user?.Email, cancellationToken);

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
                .Concat(invites)
                .OrderByDescending(x => x.CreatedAt)
                .Take(MaxAlerts)
                .ToList();

            return alerts;
        }

        /// <summary>
        /// Convites de agrônomo aguardando resposta. Casa por id <b>ou</b> por e-mail, porque o
        /// convite pode ter sido criado antes de o produtor ter conta.
        /// </summary>
        private async Task<List<UserAlertResponse>> GetPendingInvitesAsync(
            int userId,
            string? userEmail,
            CancellationToken cancellationToken)
        {
            var email = Services.EmailNormalizer.Normalize(userEmail);
            var now = DateTime.UtcNow;

            var pending = await _dbContext.AgronomistClients
                .Find(c => c.Status == AgronomistClientStatus.Pending
                           && c.InviteExpiresAt > now
                           && (c.ClientUserId == userId || c.ClientEmail == email))
                .ToListAsync(cancellationToken);

            if (pending.Count == 0) return [];

            var agronomistIds = pending.Select(c => c.AgronomistId).Distinct().ToList();
            var agronomists = await _dbContext.Users
                .Find(u => agronomistIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            var byId = agronomists.ToDictionary(u => u.Id, u => u.Name);

            return pending.Select(c => new UserAlertResponse
            {
                Id = $"invite-{c.Id}",
                Title = $"{byId.GetValueOrDefault(c.AgronomistId) ?? "Um agrônomo"} quer acompanhar " +
                        "seus laudos. Toque para responder ao convite.",
                PivotName = "—",
                AlertType = "AgronomistInvite",
                CreatedAt = c.InvitedAt,

                // Convite nunca fica "lido": ele some quando o produtor aceita ou recusa.
                IsRead = false
            }).ToList();
        }
    }
}
