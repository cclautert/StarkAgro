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
    internal static class DiagnosisPlanMapper
    {
        public static DiagnosisPlanResponse ToResponse(DiagnosisPlan p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            MonthlyPriceCents = p.MonthlyPriceCents,
            IncludedReportsPerMonth = p.IncludedReportsPerMonth,
            OveragePriceCents = p.OveragePriceCents,
            Active = p.Active
        };
    }

    public class GetDiagnosisPlansHandler : IRequestHandler<GetDiagnosisPlansRequest, List<DiagnosisPlanResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetDiagnosisPlansHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<List<DiagnosisPlanResponse>> Handle(GetDiagnosisPlansRequest request, CancellationToken cancellationToken)
        {
            var plans = await _dbContext.DiagnosisPlans.Find(_ => true).ToListAsync(cancellationToken);
            return plans.Select(DiagnosisPlanMapper.ToResponse).ToList();
        }
    }

    public class CreateDiagnosisPlanHandler : IRequestHandler<CreateDiagnosisPlanRequest, DiagnosisPlanResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreateDiagnosisPlanHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<DiagnosisPlanResponse> Handle(CreateDiagnosisPlanRequest request, CancellationToken cancellationToken)
        {
            var plan = new DiagnosisPlan
            {
                Id = await _dbContext.GetNextIdAsync(nameof(DiagnosisPlan), cancellationToken),
                Name = request.Name.Trim(),
                MonthlyPriceCents = request.MonthlyPriceCents,
                IncludedReportsPerMonth = request.IncludedReportsPerMonth,
                OveragePriceCents = request.OveragePriceCents,
                Active = request.Active
            };

            await _dbContext.DiagnosisPlans.InsertOneAsync(plan, null, cancellationToken);
            return DiagnosisPlanMapper.ToResponse(plan);
        }
    }

    public class UpdateDiagnosisPlanHandler : IRequestHandler<UpdateDiagnosisPlanRequest, DiagnosisPlanResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public UpdateDiagnosisPlanHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<DiagnosisPlanResponse> Handle(UpdateDiagnosisPlanRequest request, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.DiagnosisPlans
                .Find(p => p.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                _notifier.Handle(new Notification("Plano não encontrado."));
                return null!;
            }

            var update = Builders<DiagnosisPlan>.Update
                .Set(p => p.Name, request.Name.Trim())
                .Set(p => p.MonthlyPriceCents, request.MonthlyPriceCents)
                .Set(p => p.IncludedReportsPerMonth, request.IncludedReportsPerMonth)
                .Set(p => p.OveragePriceCents, request.OveragePriceCents)
                .Set(p => p.Active, request.Active);

            await _dbContext.DiagnosisPlans.UpdateOneAsync(p => p.Id == request.Id, update, null, cancellationToken);

            existing.Name = request.Name.Trim();
            existing.MonthlyPriceCents = request.MonthlyPriceCents;
            existing.IncludedReportsPerMonth = request.IncludedReportsPerMonth;
            existing.OveragePriceCents = request.OveragePriceCents;
            existing.Active = request.Active;
            return DiagnosisPlanMapper.ToResponse(existing);
        }
    }

    public class DeleteDiagnosisPlanHandler : IRequestHandler<DeleteDiagnosisPlanRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public DeleteDiagnosisPlanHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<bool> Handle(DeleteDiagnosisPlanRequest request, CancellationToken cancellationToken)
        {
            // Apagar um plano em uso deixaria produtores apontando para um plano fantasma e a
            // fatura sem preço. Bloqueia — o admin desativa (Active=false) em vez de apagar.
            var inUse = await _dbContext.Users
                .Find(u => u.DiagnosisPlanId == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (inUse is not null)
            {
                _notifier.Handle(new Notification(
                    "Este plano está atribuído a produtores. Desative-o em vez de apagar."));
                return false;
            }

            await _dbContext.DiagnosisPlans.DeleteOneAsync(p => p.Id == request.Id, cancellationToken);
            return true;
        }
    }
}
