using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Revenda;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services;
using StarkAgroAPI.Services.Revenda;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Revenda
{
    public class GetMyRevendaHandler : IRequestHandler<GetMyRevendaRequest, RevendaResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IRevendaMembershipService _membership;
        private readonly INotifier _notifier;

        public GetMyRevendaHandler(agpDBContext dbContext, ICurrentUserContext currentUser,
            IRevendaMembershipService membership, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _membership = membership;
            _notifier = notifier;
        }

        public async Task<RevendaResponse?> Handle(GetMyRevendaRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var revendaId = await _membership.GetManagedRevendaIdAsync(userId, cancellationToken);
            if (revendaId is null)
            {
                _notifier.Handle(new Notification("Você não gere nenhuma revenda."));
                return null;
            }

            var revenda = await _dbContext.Revendas.Find(r => r.Id == revendaId).FirstOrDefaultAsync(cancellationToken);
            if (revenda is null)
            {
                _notifier.Handle(new Notification("Revenda não encontrada."));
                return null;
            }

            return RevendaMapper.ToResponse(revenda);
        }
    }

    public class ListRevendaMembersHandler : IRequestHandler<ListRevendaMembersRequest, List<RevendaMemberResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IRevendaMembershipService _membership;

        public ListRevendaMembersHandler(agpDBContext dbContext, ICurrentUserContext currentUser,
            IRevendaMembershipService membership)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _membership = membership;
        }

        public async Task<List<RevendaMemberResponse>> Handle(ListRevendaMembersRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var revendaId = await _membership.GetManagedRevendaIdAsync(userId, cancellationToken);
            if (revendaId is null) return [];

            var members = await _dbContext.RevendaMemberships
                .Find(m => m.RevendaId == revendaId
                           && (m.Status == RevendaMembershipStatus.Active
                               || m.Status == RevendaMembershipStatus.Pending))
                .ToListAsync(cancellationToken);

            if (members.Count == 0) return [];

            var memberIds = members.Where(m => m.MemberUserId.HasValue).Select(m => m.MemberUserId!.Value).ToList();
            var users = memberIds.Count == 0
                ? []
                : await _dbContext.Users.Find(u => memberIds.Contains(u.Id)).ToListAsync(cancellationToken);
            var namesById = users.ToDictionary(u => u.Id, u => u.Name);

            return members.Select(m => new RevendaMemberResponse
            {
                Id = m.Id,
                MemberUserId = m.MemberUserId,
                MemberEmail = m.MemberEmail,
                MemberName = m.MemberUserId.HasValue ? namesById.GetValueOrDefault(m.MemberUserId.Value) : null,
                MemberRole = m.MemberRole,
                Status = m.Status,
                InvitedAt = m.InvitedAt,
                InviteExpiresAt = m.InviteExpiresAt,
                AcceptedAt = m.AcceptedAt
            }).ToList();
        }
    }

    /// <summary>
    /// Convida um agrônomo ou cliente para a revenda do gestor. Espelha <c>InviteClientHandler</c>:
    /// o convidado pode ainda não ter conta (guarda o e-mail), e a revenda vem sempre da membership
    /// do chamador — nunca do request.
    /// </summary>
    public class InviteRevendaMemberHandler : IRequestHandler<InviteRevendaMemberRequest, RevendaMemberResponse?>
    {
        private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IRevendaMembershipService _membership;
        private readonly IRevendaSeatService _seats;
        private readonly INotifier _notifier;

        public InviteRevendaMemberHandler(agpDBContext dbContext, ICurrentUserContext currentUser,
            IRevendaMembershipService membership, IRevendaSeatService seats, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _membership = membership;
            _seats = seats;
            _notifier = notifier;
        }

        public async Task<RevendaMemberResponse?> Handle(InviteRevendaMemberRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var revendaId = await _membership.GetManagedRevendaIdAsync(userId, cancellationToken);
            if (revendaId is null)
            {
                _notifier.Handle(new Notification("Você não gere nenhuma revenda."));
                return null;
            }

            // Gestor é papel do admin — o convite só cria agrônomo ou cliente.
            if (request.Role != RevendaMemberRole.Agronomist && request.Role != RevendaMemberRole.Client)
            {
                _notifier.Handle(new Notification("Papel inválido. Use Agronomist ou Client."));
                return null;
            }

            // Teto de assentos do plano. Só vale para produtor — agrônomo e gestor são equipe da
            // revenda e não ocupam assento.
            if (request.Role == RevendaMemberRole.Client)
            {
                var seats = await _seats.GetAsync(revendaId.Value, cancellationToken);
                if (seats.IsFull)
                {
                    _notifier.Handle(new Notification(
                        $"Limite de {seats.Max} produtores do plano atingido ({seats.Used} em uso, contando convites pendentes). Remova um produtor ou peça upgrade do plano."));
                    return null;
                }
            }

            var email = EmailNormalizer.Normalize(request.Email);

            var invitee = await _dbContext.Users.Find(EmailNormalizer.ByEmail(email)).FirstOrDefaultAsync(cancellationToken);
            if (invitee is not null && invitee.Id == userId)
            {
                _notifier.Handle(new Notification("Você não pode convidar a si mesmo."));
                return null;
            }

            var alreadyInvited = await _dbContext.RevendaMemberships
                .Find(m => m.RevendaId == revendaId
                           && m.MemberEmail == email
                           && (m.Status == RevendaMembershipStatus.Pending
                               || m.Status == RevendaMembershipStatus.Active))
                .FirstOrDefaultAsync(cancellationToken);
            if (alreadyInvited is not null)
            {
                _notifier.Handle(new Notification("Já existe um convite ou vínculo com este e-mail nesta revenda."));
                return null;
            }

            var now = DateTime.UtcNow;
            var membership = new RevendaMembership
            {
                Id = await _dbContext.GetNextIdAsync(nameof(RevendaMembership), cancellationToken),
                RevendaId = revendaId.Value,
                MemberRole = request.Role,
                MemberUserId = invitee?.Id,
                MemberEmail = email,
                Status = RevendaMembershipStatus.Pending,
                InviteToken = Guid.NewGuid().ToString("N"),
                InvitedAt = now,
                InviteExpiresAt = now + InviteLifetime,
                CreatedAt = now
            };

            await _dbContext.RevendaMemberships.InsertOneAsync(membership, null, cancellationToken);

            return new RevendaMemberResponse
            {
                Id = membership.Id,
                MemberUserId = membership.MemberUserId,
                MemberEmail = membership.MemberEmail,
                MemberName = invitee?.Name,
                MemberRole = membership.MemberRole,
                Status = membership.Status,
                InvitedAt = membership.InvitedAt,
                InviteExpiresAt = membership.InviteExpiresAt
            };
        }
    }

    public class RevokeRevendaMemberHandler : IRequestHandler<RevokeRevendaMemberRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IRevendaMembershipService _membership;
        private readonly INotifier _notifier;

        public RevokeRevendaMemberHandler(agpDBContext dbContext, ICurrentUserContext currentUser,
            IRevendaMembershipService membership, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _membership = membership;
            _notifier = notifier;
        }

        public async Task<bool> Handle(RevokeRevendaMemberRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var revendaId = await _membership.GetManagedRevendaIdAsync(userId, cancellationToken);
            if (revendaId is null)
            {
                _notifier.Handle(new Notification("Você não gere nenhuma revenda."));
                return false;
            }

            var link = await _dbContext.RevendaMemberships
                .Find(m => m.Id == request.LinkId && m.RevendaId == revendaId)
                .FirstOrDefaultAsync(cancellationToken);
            if (link is null)
            {
                _notifier.Handle(new Notification("Membro não encontrado nesta revenda."));
                return false;
            }

            await _dbContext.RevendaMemberships.UpdateOneAsync(
                m => m.Id == link.Id,
                Builders<RevendaMembership>.Update
                    .Set(m => m.Status, RevendaMembershipStatus.Revoked)
                    .Set(m => m.RevokedAt, DateTime.UtcNow)
                    .Set(m => m.RevokedByUserId, userId),
                null,
                cancellationToken);

            // Limpa o cache denormalizado do membro se ele apontava para esta revenda.
            if (link.MemberUserId is int memberUserId)
            {
                await _dbContext.Users.UpdateOneAsync(
                    u => u.Id == memberUserId && u.RevendaId == revendaId,
                    Builders<User>.Update.Set(u => u.RevendaId, (int?)null),
                    null,
                    cancellationToken);
            }

            return true;
        }
    }
}
