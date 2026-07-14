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
    public class GetMyAgronomistInvitesHandler
        : IRequestHandler<GetMyAgronomistInvitesRequest, List<AgronomistInviteResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetMyAgronomistInvitesHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<List<AgronomistInviteResponse>> Handle(
            GetMyAgronomistInvitesRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var me = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
            var email = Services.EmailNormalizer.Normalize(me?.Email);

            var now = DateTime.UtcNow;

            // Casa por id OU por e-mail: o convite pode ter sido criado antes de o produtor
            // ter conta, e nesse caso o ClientUserId ainda está nulo.
            var invites = await _dbContext.AgronomistClients
                .Find(c => c.Status == AgronomistClientStatus.Pending
                           && c.InviteExpiresAt > now
                           && (c.ClientUserId == userId || c.ClientEmail == email))
                .ToListAsync(cancellationToken);

            if (invites.Count == 0) return [];

            var agronomistIds = invites.Select(c => c.AgronomistId).Distinct().ToList();
            var agronomists = await _dbContext.Users
                .Find(u => agronomistIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            var byId = agronomists.ToDictionary(u => u.Id);

            return invites.Select(c => new AgronomistInviteResponse
            {
                Id = c.Id,
                AgronomistId = c.AgronomistId,
                AgronomistName = byId.GetValueOrDefault(c.AgronomistId)?.Name,
                AgronomistCrea = byId.GetValueOrDefault(c.AgronomistId)?.AgronomistCrea,
                InvitedAt = c.InvitedAt,
                InviteExpiresAt = c.InviteExpiresAt
            }).ToList();
        }
    }

    public class AcceptAgronomistInviteHandler : IRequestHandler<AcceptAgronomistInviteRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public AcceptAgronomistInviteHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<bool> Handle(AcceptAgronomistInviteRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var me = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
            var email = Services.EmailNormalizer.Normalize(me?.Email);

            var invite = await _dbContext.AgronomistClients
                .Find(c => c.Id == request.InviteId
                           && c.Status == AgronomistClientStatus.Pending
                           && (c.ClientUserId == userId || c.ClientEmail == email))
                .FirstOrDefaultAsync(cancellationToken);

            if (invite is null)
            {
                _notifier.Handle(new Notification("Convite não encontrado."));
                return false;
            }

            if (invite.InviteExpiresAt <= DateTime.UtcNow)
            {
                await _dbContext.AgronomistClients.UpdateOneAsync(
                    c => c.Id == invite.Id,
                    Builders<AgronomistClient>.Update.Set(c => c.Status, AgronomistClientStatus.Expired),
                    null,
                    cancellationToken);

                _notifier.Handle(new Notification("Este convite expirou. Peça um novo ao agrônomo."));
                return false;
            }

            var now = DateTime.UtcNow;

            // Um produtor tem um agrônomo ativo por vez: aceitar um novo convite revoga o
            // vínculo anterior. O índice único parcial no banco garante a invariante mesmo
            // sob concorrência — aqui só cuidamos da transição.
            await _dbContext.AgronomistClients.UpdateManyAsync(
                c => c.ClientUserId == userId && c.Status == AgronomistClientStatus.Active,
                Builders<AgronomistClient>.Update
                    .Set(c => c.Status, AgronomistClientStatus.Revoked)
                    .Set(c => c.RevokedAt, now)
                    .Set(c => c.RevokedByUserId, userId),
                null,
                cancellationToken);

            await _dbContext.AgronomistClients.UpdateOneAsync(
                c => c.Id == invite.Id,
                Builders<AgronomistClient>.Update
                    .Set(c => c.Status, AgronomistClientStatus.Active)
                    .Set(c => c.ClientUserId, userId)
                    .Set(c => c.AcceptedAt, now),
                null,
                cancellationToken);

            return true;
        }
    }

    public class DeclineAgronomistInviteHandler : IRequestHandler<DeclineAgronomistInviteRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public DeclineAgronomistInviteHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<bool> Handle(DeclineAgronomistInviteRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var me = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
            var email = Services.EmailNormalizer.Normalize(me?.Email);

            var result = await _dbContext.AgronomistClients.UpdateOneAsync(
                c => c.Id == request.InviteId
                     && c.Status == AgronomistClientStatus.Pending
                     && (c.ClientUserId == userId || c.ClientEmail == email),
                Builders<AgronomistClient>.Update.Set(c => c.Status, AgronomistClientStatus.Declined),
                null,
                cancellationToken);

            if (result.ModifiedCount == 0)
            {
                _notifier.Handle(new Notification("Convite não encontrado."));
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// O produtor demite o agrônomo. Esse direito é dele e não passa por ninguém —
    /// a partir daqui o agrônomo perde acesso aos laudos imediatamente.
    /// </summary>
    public class RevokeMyAgronomistHandler : IRequestHandler<RevokeMyAgronomistRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public RevokeMyAgronomistHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<bool> Handle(RevokeMyAgronomistRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var result = await _dbContext.AgronomistClients.UpdateManyAsync(
                c => c.ClientUserId == userId && c.Status == AgronomistClientStatus.Active,
                Builders<AgronomistClient>.Update
                    .Set(c => c.Status, AgronomistClientStatus.Revoked)
                    .Set(c => c.RevokedAt, DateTime.UtcNow)
                    .Set(c => c.RevokedByUserId, userId),
                null,
                cancellationToken);

            if (result.ModifiedCount == 0)
            {
                _notifier.Handle(new Notification("Você não tem um agrônomo vinculado."));
                return false;
            }

            return true;
        }
    }
}
