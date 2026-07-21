using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    internal static class RevendaMapper
    {
        public static RevendaResponse ToResponse(Models.Entities.Revenda r) => new()
        {
            Id = r.Id,
            Name = r.Name,
            Cnpj = r.Cnpj,
            ContactEmail = r.ContactEmail,
            DiagnosisPlanId = r.DiagnosisPlanId,
            DiagnosisQuotaPerMonth = r.DiagnosisQuotaPerMonth,
            Active = r.Active,
            CreatedAt = r.CreatedAt
        };
    }

    public class GetRevendasHandler : IRequestHandler<GetRevendasRequest, List<RevendaResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetRevendasHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<List<RevendaResponse>> Handle(GetRevendasRequest request, CancellationToken cancellationToken)
        {
            var revendas = await _dbContext.Revendas.Find(_ => true).ToListAsync(cancellationToken);
            return revendas.Select(RevendaMapper.ToResponse).ToList();
        }
    }

    public class CreateRevendaHandler : IRequestHandler<CreateRevendaRequest, RevendaResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public CreateRevendaHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<RevendaResponse> Handle(CreateRevendaRequest request, CancellationToken cancellationToken)
        {
            if (!await PlanExistsAsync(_dbContext, request.DiagnosisPlanId, _notifier, cancellationToken))
                return null!;

            var revenda = new Models.Entities.Revenda
            {
                Id = await _dbContext.GetNextIdAsync(nameof(Models.Entities.Revenda), cancellationToken),
                Name = request.Name.Trim(),
                Cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : request.Cnpj.Trim(),
                ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim(),
                DiagnosisPlanId = request.DiagnosisPlanId,
                DiagnosisQuotaPerMonth = request.DiagnosisQuotaPerMonth,
                Active = request.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = _currentUser.UserId ?? 0
            };

            await _dbContext.Revendas.InsertOneAsync(revenda, null, cancellationToken);
            return RevendaMapper.ToResponse(revenda);
        }

        internal static async Task<bool> PlanExistsAsync(
            agpDBContext dbContext, int? planId, INotifier notifier, CancellationToken cancellationToken)
        {
            if (planId is not int id) return true; // sem plano é válido
            var plan = await dbContext.DiagnosisPlans.Find(p => p.Id == id).FirstOrDefaultAsync(cancellationToken);
            if (plan is null)
            {
                notifier.Handle(new Notification("Plano informado não existe."));
                return false;
            }
            return true;
        }
    }

    public class UpdateRevendaHandler : IRequestHandler<UpdateRevendaRequest, RevendaResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public UpdateRevendaHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<RevendaResponse> Handle(UpdateRevendaRequest request, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.Revendas
                .Find(r => r.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                _notifier.Handle(new Notification("Revenda não encontrada."));
                return null!;
            }

            if (!await CreateRevendaHandler.PlanExistsAsync(_dbContext, request.DiagnosisPlanId, _notifier, cancellationToken))
                return null!;

            var name = request.Name.Trim();
            var cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : request.Cnpj.Trim();
            var contactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();

            var update = Builders<Models.Entities.Revenda>.Update
                .Set(r => r.Name, name)
                .Set(r => r.Cnpj, cnpj)
                .Set(r => r.ContactEmail, contactEmail)
                .Set(r => r.DiagnosisPlanId, request.DiagnosisPlanId)
                .Set(r => r.DiagnosisQuotaPerMonth, request.DiagnosisQuotaPerMonth)
                .Set(r => r.Active, request.Active);

            await _dbContext.Revendas.UpdateOneAsync(r => r.Id == request.Id, update, null, cancellationToken);

            existing.Name = name;
            existing.Cnpj = cnpj;
            existing.ContactEmail = contactEmail;
            existing.DiagnosisPlanId = request.DiagnosisPlanId;
            existing.DiagnosisQuotaPerMonth = request.DiagnosisQuotaPerMonth;
            existing.Active = request.Active;
            return RevendaMapper.ToResponse(existing);
        }
    }

    public class AssignRevendaManagerHandler : IRequestHandler<AssignRevendaManagerRequest, RevendaResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public AssignRevendaManagerHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<RevendaResponse> Handle(AssignRevendaManagerRequest request, CancellationToken cancellationToken)
        {
            var revenda = await _dbContext.Revendas
                .Find(r => r.Id == request.RevendaId)
                .FirstOrDefaultAsync(cancellationToken);
            if (revenda is null)
            {
                _notifier.Handle(new Notification("Revenda não encontrada."));
                return null!;
            }

            var user = await _dbContext.Users
                .Find(u => u.Id == request.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (user is null)
            {
                _notifier.Handle(new Notification("Usuário não encontrado."));
                return null!;
            }

            // Idempotente: só cria a membership Manager se ainda não houver uma ativa.
            var alreadyManager = await _dbContext.RevendaMemberships
                .Find(m => m.RevendaId == revenda.Id
                    && m.MemberUserId == user.Id
                    && m.MemberRole == RevendaMemberRole.Manager
                    && m.Status == RevendaMembershipStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            if (alreadyManager is null)
            {
                var now = DateTime.UtcNow;
                var membership = new RevendaMembership
                {
                    Id = await _dbContext.GetNextIdAsync(nameof(RevendaMembership), cancellationToken),
                    RevendaId = revenda.Id,
                    MemberRole = RevendaMemberRole.Manager,
                    MemberUserId = user.Id,
                    MemberEmail = user.Email,
                    Status = RevendaMembershipStatus.Active,
                    InvitedAt = now,
                    InviteExpiresAt = now,
                    AcceptedAt = now,
                    CreatedAt = now
                };
                await _dbContext.RevendaMemberships.InsertOneAsync(membership, null, cancellationToken);
            }

            // Papel via AddToSet: idempotente e sem sobrescrever os demais papéis do usuário.
            var roleUpdate = Builders<User>.Update.AddToSet(u => u.Roles, UserRole.ResellerManager);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, roleUpdate, null, cancellationToken);

            return RevendaMapper.ToResponse(revenda);
        }
    }
}
