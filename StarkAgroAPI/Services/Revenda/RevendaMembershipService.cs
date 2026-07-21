using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Revenda
{
    /// <summary>
    /// Regra única de "qual revenda o usuário gere". Todo handler de gestor resolve a revenda
    /// pelo chamador aqui — nunca pelo request — do mesmo jeito que <c>IDiagnosisAccessService</c>
    /// centraliza o acesso a laudo. É o que impede um gestor tocar membros de outra revenda.
    /// </summary>
    public interface IRevendaMembershipService
    {
        /// <summary>Revenda em que <paramref name="userId"/> é gestor ativo, ou null.</summary>
        Task<int?> GetManagedRevendaIdAsync(int userId, CancellationToken cancellationToken);
    }

    public class RevendaMembershipService : IRevendaMembershipService
    {
        private readonly agpDBContext _dbContext;

        public RevendaMembershipService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<int?> GetManagedRevendaIdAsync(int userId, CancellationToken cancellationToken)
        {
            var membership = await _dbContext.RevendaMemberships
                .Find(m => m.MemberUserId == userId
                           && m.MemberRole == RevendaMemberRole.Manager
                           && m.Status == RevendaMembershipStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            return membership?.RevendaId;
        }
    }
}
