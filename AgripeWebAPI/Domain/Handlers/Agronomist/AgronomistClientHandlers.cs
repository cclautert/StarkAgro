using AgripeWebAPI.Domain.Commands.Requests.Agronomist;
using AgripeWebAPI.Domain.Commands.Responses.Agronomist;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Agronomist
{
    public class GetAgronomistClientsHandler
        : IRequestHandler<GetAgronomistClientsRequest, List<AgronomistClientResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetAgronomistClientsHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<List<AgronomistClientResponse>> Handle(
            GetAgronomistClientsRequest request,
            CancellationToken cancellationToken)
        {
            var agronomistId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var links = await _dbContext.AgronomistClients
                .Find(c => c.AgronomistId == agronomistId
                           && (c.Status == AgronomistClientStatus.Active
                               || c.Status == AgronomistClientStatus.Pending))
                .ToListAsync(cancellationToken);

            if (links.Count == 0) return [];

            var clientIds = links.Where(c => c.ClientUserId.HasValue)
                .Select(c => c.ClientUserId!.Value)
                .ToList();

            var clients = clientIds.Count == 0
                ? []
                : await _dbContext.Users.Find(u => clientIds.Contains(u.Id)).ToListAsync(cancellationToken);

            var namesById = clients.ToDictionary(u => u.Id, u => u.Name);

            var pendingByClient = new Dictionary<int, int>();
            if (clientIds.Count > 0)
            {
                var pending = await _dbContext.PlantDiagnoses
                    .Find(d => d.AgronomistId == agronomistId
                               && clientIds.Contains(d.UserId)
                               && d.Status == PlantDiagnosisStatus.PendingReview)
                    .ToListAsync(cancellationToken);

                pendingByClient = pending
                    .GroupBy(d => d.UserId)
                    .ToDictionary(g => g.Key, g => g.Count());
            }

            return links.Select(c => new AgronomistClientResponse
            {
                Id = c.Id,
                ClientUserId = c.ClientUserId,
                ClientEmail = c.ClientEmail,
                ClientName = c.ClientUserId.HasValue ? namesById.GetValueOrDefault(c.ClientUserId.Value) : null,
                Status = c.Status,
                InvitedAt = c.InvitedAt,
                InviteExpiresAt = c.InviteExpiresAt,
                AcceptedAt = c.AcceptedAt,
                PendingDiagnoses = c.ClientUserId.HasValue
                    ? pendingByClient.GetValueOrDefault(c.ClientUserId.Value)
                    : 0
            }).ToList();
        }
    }

    /// <summary>
    /// Convida um produtor. O convidado pode <b>ainda não ter conta</b> — por isso o vínculo
    /// guarda o e-mail e só resolve o <c>ClientUserId</c> quando ele existe.
    /// </summary>
    public class InviteClientHandler : IRequestHandler<InviteClientRequest, AgronomistClientResponse?>
    {
        private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public InviteClientHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<AgronomistClientResponse?> Handle(
            InviteClientRequest request,
            CancellationToken cancellationToken)
        {
            var agronomistId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var email = request.ClientEmail.Trim().ToLowerInvariant();

            var client = await _dbContext.Users
                .Find(u => u.Email == email)
                .FirstOrDefaultAsync(cancellationToken);

            if (client is not null && client.Id == agronomistId)
            {
                _notifier.Handle(new Notification("Você não pode convidar a si mesmo."));
                return null;
            }

            var alreadyInvited = await _dbContext.AgronomistClients
                .Find(c => c.AgronomistId == agronomistId
                           && c.ClientEmail == email
                           && (c.Status == AgronomistClientStatus.Pending
                               || c.Status == AgronomistClientStatus.Active))
                .FirstOrDefaultAsync(cancellationToken);

            if (alreadyInvited is not null)
            {
                _notifier.Handle(new Notification("Já existe um convite ou vínculo com este e-mail."));
                return null;
            }

            var now = DateTime.UtcNow;
            var link = new AgronomistClient
            {
                Id = await _dbContext.GetNextIdAsync(nameof(AgronomistClient), cancellationToken),
                AgronomistId = agronomistId,
                ClientUserId = client?.Id,
                ClientEmail = email,
                Status = AgronomistClientStatus.Pending,
                InviteToken = Guid.NewGuid().ToString("N"),
                InvitedAt = now,
                InviteExpiresAt = now + InviteLifetime,
                CreatedAt = now
            };

            await _dbContext.AgronomistClients.InsertOneAsync(link, cancellationToken: cancellationToken);

            return new AgronomistClientResponse
            {
                Id = link.Id,
                ClientUserId = link.ClientUserId,
                ClientEmail = link.ClientEmail,
                ClientName = client?.Name,
                Status = link.Status,
                InvitedAt = link.InvitedAt,
                InviteExpiresAt = link.InviteExpiresAt
            };
        }
    }

    public class RevokeClientHandler : IRequestHandler<RevokeClientRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public RevokeClientHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<bool> Handle(RevokeClientRequest request, CancellationToken cancellationToken)
        {
            var agronomistId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var link = await _dbContext.AgronomistClients
                .Find(c => c.Id == request.LinkId && c.AgronomistId == agronomistId)
                .FirstOrDefaultAsync(cancellationToken);

            if (link is null)
            {
                _notifier.Handle(new Notification("Vínculo não encontrado."));
                return false;
            }

            // Revogar preserva a história: o vínculo vira Revoked, não é apagado. A auditoria
            // de um laudo assinado precisa saber quem era o agrônomo responsável naquela data.
            await _dbContext.AgronomistClients.UpdateOneAsync(
                c => c.Id == link.Id,
                Builders<AgronomistClient>.Update
                    .Set(c => c.Status, AgronomistClientStatus.Revoked)
                    .Set(c => c.RevokedAt, DateTime.UtcNow)
                    .Set(c => c.RevokedByUserId, agronomistId),
                null,
                cancellationToken);

            return true;
        }
    }
}
