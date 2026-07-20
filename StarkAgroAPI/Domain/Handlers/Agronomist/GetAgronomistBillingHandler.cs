using StarkAgroAPI.Domain.Commands.Requests.Agronomist;
using StarkAgroAPI.Domain.Commands.Responses.Agronomist;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Diagnosis;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Agronomist
{
    /// <summary>
    /// Painel de faturamento do agrônomo: por cliente ativo da carteira, quanto consumiu e deve
    /// no mês.
    /// <para>
    /// Isolamento: só entram clientes com vínculo <b>Active</b> deste agrônomo — a mesma regra da
    /// carteira. O agrônomo lê faturamento derivado dos laudos (que ele já enxerga), não pivôs
    /// nem sensores do cliente. Nada aqui fura o <c>ContextSnapshot</c>.
    /// </para>
    /// </summary>
    public class GetAgronomistBillingHandler
        : IRequestHandler<GetAgronomistBillingRequest, AgronomistBillingResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisBillingService _billing;

        public GetAgronomistBillingHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisBillingService billing)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _billing = billing ?? throw new ArgumentNullException(nameof(billing));
        }

        public async Task<AgronomistBillingResponse> Handle(
            GetAgronomistBillingRequest request,
            CancellationToken cancellationToken)
        {
            var agronomistId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated agronomist is required.");

            // Só clientes ativos e já com conta (ClientUserId resolvido) têm consumo a faturar.
            var links = await _dbContext.AgronomistClients
                .Find(c => c.AgronomistId == agronomistId
                           && c.Status == AgronomistClientStatus.Active
                           && c.ClientUserId != null)
                .ToListAsync(cancellationToken);

            var response = new AgronomistBillingResponse();
            if (links.Count == 0)
            {
                var now = DateTime.UtcNow;
                response.PeriodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                response.PeriodEnd = response.PeriodStart.AddMonths(1);
                return response;
            }

            var clientIds = links.Select(c => c.ClientUserId!.Value).Distinct().ToList();
            var clients = await _dbContext.Users
                .Find(u => clientIds.Contains(u.Id))
                .ToListAsync(cancellationToken);
            var byId = clients.ToDictionary(u => u.Id);

            foreach (var clientId in clientIds)
            {
                var invoice = await _billing.GetProducerInvoiceAsync(clientId, cancellationToken);
                byId.TryGetValue(clientId, out var user);

                response.Clients.Add(new AgronomistBillingLine
                {
                    ClientUserId = clientId,
                    ClientName = user?.Name,
                    ClientEmail = user?.Email,
                    PlanName = invoice.PlanName,
                    MonthlyPriceCents = invoice.MonthlyPriceCents,
                    IncludedReports = invoice.IncludedReports,
                    UsedReports = invoice.UsedReports,
                    OverageReports = invoice.OverageReports,
                    OveragePriceCents = invoice.OveragePriceCents,
                    TotalCents = invoice.TotalCents
                });

                response.PeriodStart = invoice.PeriodStart;
                response.PeriodEnd = invoice.PeriodEnd;
            }

            response.TotalCents = response.Clients.Sum(c => c.TotalCents);
            return response;
        }
    }
}
