using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using MongoDB.Driver;

namespace StarkAgroAPI.Services.Diagnosis
{
    /// <param name="Limit">Laudos permitidos no mês. <c>0</c> = ilimitado.</param>
    /// <param name="Used">Laudos já enviados no mês corrente.</param>
    /// <param name="ResetsAt">Início do próximo mês (UTC) — quando a cota zera.</param>
    public record DiagnosisQuota(int Limit, int Used, DateTime ResetsAt)
    {
        public bool IsUnlimited => Limit <= 0;

        public int Remaining => IsUnlimited ? int.MaxValue : Math.Max(0, Limit - Used);

        public bool IsExhausted => !IsUnlimited && Used >= Limit;
    }

    /// <summary>
    /// Cota mensal de laudos do produtor.
    /// <para>
    /// Existe por dois motivos, e o segundo é o que importa: dá lastro ao plano contratado, e
    /// impede que um único produtor queime os créditos de IA de todo mundo. Cada foto analisada
    /// é uma chamada paga ao classificador.
    /// </para>
    /// </summary>
    public interface IDiagnosisQuotaService
    {
        Task<DiagnosisQuota> GetAsync(int userId, CancellationToken cancellationToken);
    }

    public class DiagnosisQuotaService : IDiagnosisQuotaService
    {
        private readonly agpDBContext _dbContext;

        public DiagnosisQuotaService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<DiagnosisQuota> GetAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);

            var settings = await _dbContext.PlatformAiSettings
                .Find(_ => true)
                .FirstOrDefaultAsync(cancellationToken);

            // Precedência: cota do usuário → cota da revenda que o paga → padrão da plataforma.
            // Quem banca a conta é quem tem direito de limitar o consumo.
            var revendaQuota = user is null ? null : await GetRevendaQuotaAsync(user.Id, cancellationToken);
            var limit = user?.DiagnosisQuotaPerMonth
                        ?? revendaQuota
                        ?? settings?.DefaultDiagnosisQuotaPerMonth
                        ?? 0;

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonth = monthStart.AddMonths(1);

            if (limit <= 0)
            {
                return new DiagnosisQuota(0, 0, nextMonth);
            }

            // Conta o que foi ENVIADO no mês, não o que deu certo: a foto recusada também
            // consumiu uma chamada paga ao classificador.
            var used = await _dbContext.PlantDiagnoses
                .Find(d => d.UserId == userId && d.CreatedAt >= monthStart && d.CreatedAt < nextMonth)
                .ToListAsync(cancellationToken);

            return new DiagnosisQuota(limit, used.Count, nextMonth);
        }

        /// <summary>
        /// Cota-padrão da revenda que paga por este produtor, ou null se ele não é membro de nenhuma.
        /// <para>
        /// A revenda vem do vínculo <c>Client</c> Active — fonte da verdade —, não de
        /// <c>User.RevendaId</c>, que é só cache denormalizado e pode ficar para trás.
        /// </para>
        /// </summary>
        private async Task<int?> GetRevendaQuotaAsync(int userId, CancellationToken cancellationToken)
        {
            var membership = await _dbContext.RevendaMemberships
                .Find(m => m.MemberUserId == userId
                           && m.MemberRole == RevendaMemberRole.Client
                           && m.Status == RevendaMembershipStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            if (membership is null) return null;

            var revenda = await _dbContext.Revendas
                .Find(r => r.Id == membership.RevendaId)
                .FirstOrDefaultAsync(cancellationToken);

            return revenda?.DiagnosisQuotaPerMonth;
        }
    }
}
