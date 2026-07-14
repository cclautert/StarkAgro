using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MongoDB.Driver;

namespace AgripeWebAPI.Services.Diagnosis
{
    /// <summary>
    /// A regra de acesso ao laudo, implementada <b>uma vez só</b>.
    /// <para>
    /// <b>Leitura:</b> o usuário <c>u</c> lê o laudo <c>d</c> se e somente se
    /// <c>d.UserId == u</c> (é o produtor dono) <b>OU</b> (<c>d.AgronomistId == u</c>
    /// <b>E</b> existe um vínculo <c>Active</c> entre <c>u</c> e <c>d.UserId</c>).
    /// </para>
    /// <para>
    /// A dupla condição é deliberada: o <c>AgronomistId</c> denormalizado dá a query rápida,
    /// e a checagem do vínculo ativo faz a <b>revogação ter efeito imediato</b>. É o que evita
    /// o bug clássico "revoguei o agrônomo e ele continua vendo meus laudos".
    /// </para>
    /// <para>
    /// <b>Admin não tem furo aqui.</b> Um laudo é ato profissional; não faz sentido um
    /// administrador lê-lo ou assiná-lo.
    /// </para>
    /// </summary>
    public interface IDiagnosisAccessService
    {
        Task<bool> CanAccessAsync(int userId, PlantDiagnosis diagnosis, CancellationToken cancellationToken);

        /// <summary>Ids dos produtores com vínculo ativo — usado para montar a fila do agrônomo.</summary>
        Task<IReadOnlyList<int>> GetActiveClientIdsAsync(int agronomistId, CancellationToken cancellationToken);

        Task<bool> HasActiveLinkAsync(int agronomistId, int clientUserId, CancellationToken cancellationToken);

        /// <summary>Vínculo ativo do produtor, se houver — usado ao criar o laudo.</summary>
        Task<AgronomistClient?> GetActiveLinkForClientAsync(int clientUserId, CancellationToken cancellationToken);
    }

    public class DiagnosisAccessService : IDiagnosisAccessService
    {
        private readonly agpDBContext _dbContext;

        public DiagnosisAccessService(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<bool> CanAccessAsync(
            int userId,
            PlantDiagnosis diagnosis,
            CancellationToken cancellationToken)
        {
            if (diagnosis.UserId == userId) return true;

            if (diagnosis.AgronomistId != userId) return false;

            return await HasActiveLinkAsync(userId, diagnosis.UserId, cancellationToken);
        }

        public async Task<bool> HasActiveLinkAsync(
            int agronomistId,
            int clientUserId,
            CancellationToken cancellationToken)
        {
            var link = await _dbContext.AgronomistClients
                .Find(c => c.AgronomistId == agronomistId
                           && c.ClientUserId == clientUserId
                           && c.Status == AgronomistClientStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

            return link is not null;
        }

        public async Task<IReadOnlyList<int>> GetActiveClientIdsAsync(
            int agronomistId,
            CancellationToken cancellationToken)
        {
            var links = await _dbContext.AgronomistClients
                .Find(c => c.AgronomistId == agronomistId
                           && c.Status == AgronomistClientStatus.Active)
                .ToListAsync(cancellationToken);

            return links
                .Where(c => c.ClientUserId.HasValue)
                .Select(c => c.ClientUserId!.Value)
                .ToList();
        }

        public async Task<AgronomistClient?> GetActiveLinkForClientAsync(
            int clientUserId,
            CancellationToken cancellationToken)
        {
            return await _dbContext.AgronomistClients
                .Find(c => c.ClientUserId == clientUserId
                           && c.Status == AgronomistClientStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
