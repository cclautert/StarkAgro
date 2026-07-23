using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Commands.Responses.Users;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Users
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

            // Focos de calor do tenant na janela (síntese agrupada mais abaixo, com readAt).
            var fireHotspots = await _dbContext.FireHotspots
                .Find(x => x.UserId == userId && x.AcquiredAt >= since)
                .ToListAsync(cancellationToken);

            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);
            var readAt = user?.AlertsReadAt;

            // Convite de agrônomo pendente. Sem isto ele só aparecia na tela de Laudos — e o
            // produtor não tem motivo nenhum para abrir aquela tela sabendo que foi convidado.
            var invites = await GetPendingInvitesAsync(userId, user?.Email, cancellationToken);

            // Mesma lógica para o convite de revenda: sem o sininho, o membro só chegaria em
            // /revenda/convites por URL.
            var revendaInvites = await GetPendingRevendaInvitesAsync(userId, user?.Email, cancellationToken);

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
                .Concat(revendaInvites)
                .Concat(await BuildFireAlertsAsync(fireHotspots, readAt, cancellationToken))
                .OrderByDescending(x => x.CreatedAt)
                .Take(MaxAlerts)
                .ToList();

            return alerts;
        }

        /// <summary>
        /// Focos de calor → alertas, <b>agrupados por (área, dia)</b>: um incêndio com dezenas de
        /// focos vira uma linha só ("N foco(s)…"), não dezenas. Tenant já filtrado na query.
        /// </summary>
        private async Task<List<UserAlertResponse>> BuildFireAlertsAsync(
            List<FireHotspot> hotspots, DateTime? readAt, CancellationToken cancellationToken)
        {
            if (hotspots.Count == 0) return [];

            var areaIds = hotspots.Select(h => h.AreaId).Distinct().ToList();
            var areas = await _dbContext.MonitoredAreas
                .Find(a => areaIds.Contains(a.Id))
                .ToListAsync(cancellationToken);
            var areaNameById = areas.ToDictionary(a => a.Id, a => a.Name);

            string AreaName(int id) =>
                areaNameById.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n) ? n : $"Área {id}";

            return hotspots
                .GroupBy(h => (h.AreaId, Day: h.AcquiredAt.Date))
                .Select(g =>
                {
                    var latest = g.Max(h => h.AcquiredAt);
                    var count = g.Count();
                    return new UserAlertResponse
                    {
                        Id = $"fire-{g.Key.AreaId}-{g.Key.Day:yyyyMMdd}",
                        Title = $"🔥 {count} foco(s) de calor perto de {AreaName(g.Key.AreaId)} (satélite, ~1h)",
                        PivotName = AreaName(g.Key.AreaId),
                        AlertType = "FireHotspot",
                        CreatedAt = latest,
                        IsRead = readAt.HasValue && latest <= readAt.Value
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Convites de revenda aguardando resposta. Casa por id <b>ou</b> por e-mail (o convite
        /// pode ter sido criado antes de o membro ter conta), igual ao convite de agrônomo.
        /// </summary>
        private async Task<List<UserAlertResponse>> GetPendingRevendaInvitesAsync(
            int userId,
            string? userEmail,
            CancellationToken cancellationToken)
        {
            var email = Services.EmailNormalizer.Normalize(userEmail);
            var now = DateTime.UtcNow;

            var pending = await _dbContext.RevendaMemberships
                .Find(m => m.Status == RevendaMembershipStatus.Pending
                           && m.InviteExpiresAt > now
                           && (m.MemberUserId == userId || m.MemberEmail == email))
                .ToListAsync(cancellationToken);

            if (pending.Count == 0) return [];

            var revendaIds = pending.Select(m => m.RevendaId).Distinct().ToList();
            var revendas = await _dbContext.Revendas
                .Find(r => revendaIds.Contains(r.Id))
                .ToListAsync(cancellationToken);
            var namesById = revendas.ToDictionary(r => r.Id, r => r.Name);

            return pending.Select(m => new UserAlertResponse
            {
                Id = $"revenda-invite-{m.Id}",
                Title = $"{namesById.GetValueOrDefault(m.RevendaId) ?? "Uma revenda"} te convidou. " +
                        "Toque para responder ao convite.",
                PivotName = "—",
                AlertType = "RevendaInvite",

                // Convite não fica "lido": some quando o membro aceita ou recusa.
                CreatedAt = m.InvitedAt,
                IsRead = false
            }).ToList();
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
