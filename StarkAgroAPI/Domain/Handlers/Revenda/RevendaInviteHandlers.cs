using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Revenda
{
    public class GetMyRevendaInvitesHandler : IRequestHandler<GetMyRevendaInvitesRequest, List<RevendaInviteResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetMyRevendaInvitesHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        public async Task<List<RevendaInviteResponse>> Handle(GetMyRevendaInvitesRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var me = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
            var email = EmailNormalizer.Normalize(me?.Email);
            var now = DateTime.UtcNow;

            // Casa por id OU e-mail: o convite pode ter sido criado antes de o membro ter conta.
            var invites = await _dbContext.RevendaMemberships
                .Find(m => m.Status == RevendaMembershipStatus.Pending
                           && m.InviteExpiresAt > now
                           && (m.MemberUserId == userId || m.MemberEmail == email))
                .ToListAsync(cancellationToken);

            if (invites.Count == 0) return [];

            var revendaIds = invites.Select(m => m.RevendaId).Distinct().ToList();
            var revendas = await _dbContext.Revendas.Find(r => revendaIds.Contains(r.Id)).ToListAsync(cancellationToken);
            var namesById = revendas.ToDictionary(r => r.Id, r => r.Name);

            return invites.Select(m => new RevendaInviteResponse
            {
                Id = m.Id,
                RevendaId = m.RevendaId,
                RevendaName = namesById.GetValueOrDefault(m.RevendaId),
                MemberRole = m.MemberRole,
                InvitedAt = m.InvitedAt,
                InviteExpiresAt = m.InviteExpiresAt
            }).ToList();
        }
    }

    public class AcceptRevendaInviteHandler : IRequestHandler<AcceptRevendaInviteRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public AcceptRevendaInviteHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<bool> Handle(AcceptRevendaInviteRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var me = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
            var email = EmailNormalizer.Normalize(me?.Email);

            var invite = await _dbContext.RevendaMemberships
                .Find(m => m.Id == request.InviteId
                           && m.Status == RevendaMembershipStatus.Pending
                           && (m.MemberUserId == userId || m.MemberEmail == email))
                .FirstOrDefaultAsync(cancellationToken);
            if (invite is null)
            {
                _notifier.Handle(new Notification("Convite não encontrado."));
                return false;
            }

            var now = DateTime.UtcNow;
            if (invite.InviteExpiresAt <= now)
            {
                await _dbContext.RevendaMemberships.UpdateOneAsync(
                    m => m.Id == invite.Id,
                    Builders<RevendaMembership>.Update.Set(m => m.Status, RevendaMembershipStatus.Expired),
                    null,
                    cancellationToken);
                _notifier.Handle(new Notification("Este convite expirou. Peça um novo à revenda."));
                return false;
            }

            // Um produtor tem uma revenda ativa por vez: aceitar um novo vínculo Client revoga o
            // anterior. O índice único parcial garante a invariante sob concorrência; aqui é só a transição.
            if (invite.MemberRole == RevendaMemberRole.Client)
            {
                await _dbContext.RevendaMemberships.UpdateManyAsync(
                    m => m.MemberUserId == userId
                         && m.MemberRole == RevendaMemberRole.Client
                         && m.Status == RevendaMembershipStatus.Active,
                    Builders<RevendaMembership>.Update
                        .Set(m => m.Status, RevendaMembershipStatus.Revoked)
                        .Set(m => m.RevokedAt, now)
                        .Set(m => m.RevokedByUserId, userId),
                    null,
                    cancellationToken);
            }

            await _dbContext.RevendaMemberships.UpdateOneAsync(
                m => m.Id == invite.Id,
                Builders<RevendaMembership>.Update
                    .Set(m => m.Status, RevendaMembershipStatus.Active)
                    .Set(m => m.MemberUserId, userId)
                    .Set(m => m.AcceptedAt, now),
                null,
                cancellationToken);

            // Cache denormalizado da revenda no usuário.
            await _dbContext.Users.UpdateOneAsync(
                u => u.Id == userId,
                Builders<User>.Update.Set(u => u.RevendaId, invite.RevendaId),
                null,
                cancellationToken);

            return true;
        }
    }

    public class DeclineRevendaInviteHandler : IRequestHandler<DeclineRevendaInviteRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public DeclineRevendaInviteHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<bool> Handle(DeclineRevendaInviteRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var me = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
            var email = EmailNormalizer.Normalize(me?.Email);

            var result = await _dbContext.RevendaMemberships.UpdateOneAsync(
                m => m.Id == request.InviteId
                     && m.Status == RevendaMembershipStatus.Pending
                     && (m.MemberUserId == userId || m.MemberEmail == email),
                Builders<RevendaMembership>.Update.Set(m => m.Status, RevendaMembershipStatus.Declined),
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
}
