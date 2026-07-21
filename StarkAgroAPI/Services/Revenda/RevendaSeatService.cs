using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Revenda
{
    /// <param name="Used">Produtores ocupando assento agora.</param>
    /// <param name="Included">Assentos inclusos na mensalidade do plano.</param>
    /// <param name="Max">Teto duro do plano. <c>0</c> = ilimitado.</param>
    public record RevendaSeats(int Used, int Included, int Max)
    {
        public bool IsUnlimited => Max <= 0;

        /// <summary>Assentos além do incluso — é o que entra na fatura.</summary>
        public int Overage => Math.Max(0, Used - Included);

        /// <summary>No teto: convite de novo produtor deve ser recusado.</summary>
        public bool IsFull => !IsUnlimited && Used >= Max;
    }

    /// <summary>
    /// Quantos produtores a revenda ocupa e quantos o plano permite.
    /// <para>
    /// Assento é o eixo <b>comercial</b> do plano, ao lado do eixo de <b>custo</b> (laudo): sem ele
    /// uma revenda no plano mais barato monta uma base inteira e a fatura só reage se o consumo de
    /// laudos estourar.
    /// </para>
    /// </summary>
    public interface IRevendaSeatService
    {
        Task<RevendaSeats> GetAsync(int revendaId, CancellationToken cancellationToken);
    }

    public class RevendaSeatService : IRevendaSeatService
    {
        private readonly agpDBContext _dbContext;

        public RevendaSeatService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Regra pura de "este vínculo ocupa assento?".
        /// <para>
        /// Assento é vínculo de <b>produtor</b>: gestor e agrônomo são equipe da revenda e não contam.
        /// Pending ocupa vaga — senão dá para furar o teto convidando em massa —, mas só enquanto o
        /// convite vale: o status só vira <c>Expired</c> quando alguém tenta aceitar, então um convite
        /// morto seguraria o assento para sempre.
        /// </para>
        /// </summary>
        public static bool OccupiesSeat(RevendaMembership m, DateTime now) =>
            m.MemberRole == RevendaMemberRole.Client
            && (m.Status == RevendaMembershipStatus.Active
                || (m.Status == RevendaMembershipStatus.Pending && m.InviteExpiresAt > now));

        public async Task<RevendaSeats> GetAsync(int revendaId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var links = await _dbContext.RevendaMemberships
                .Find(m => m.RevendaId == revendaId)
                .ToListAsync(cancellationToken);
            var used = links.Count(m => OccupiesSeat(m, now));

            var revenda = await _dbContext.Revendas
                .Find(r => r.Id == revendaId)
                .FirstOrDefaultAsync(cancellationToken);

            var plan = revenda?.DiagnosisPlanId is int planId
                ? await _dbContext.DiagnosisPlans.Find(p => p.Id == planId).FirstOrDefaultAsync(cancellationToken)
                : null;

            // Sem plano não há teto nem assento incluso — a revenda ainda não foi vendida.
            return new RevendaSeats(used, plan?.IncludedMembers ?? 0, plan?.MaxMembers ?? 0);
        }
    }
}
