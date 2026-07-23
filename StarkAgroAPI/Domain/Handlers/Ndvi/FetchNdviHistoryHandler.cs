using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using MediatR;
using MongoDB.Driver;
using System.Globalization;

namespace StarkAgroAPI.Domain.Handlers.Ndvi
{
    /// <summary>
    /// Busca retroativa sob demanda de uma passagem NDVI, para o usuário "voltar no tempo". Ordem
    /// dos guardas (barato → caro): posse da área → data no passado → curto-circuito grátis se a
    /// janela já está armazenada → teto mensal de PU → só então a chamada paga à CDSE. O custo é
    /// gravado por leitura (contado pelo <see cref="INdviCostService"/> e visto em <c>/admin/ia</c>);
    /// re-consultar a mesma data é grátis (o curto-circuito e o dedup por índice único a pegam).
    /// </summary>
    public class FetchNdviHistoryHandler : IRequestHandler<FetchNdviHistoryRequest, FetchNdviHistoryResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;
        private readonly INdviFetchService _fetchService;
        private readonly INdviCostService _costService;

        public FetchNdviHistoryHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier,
            INdviFetchService fetchService,
            INdviCostService costService)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
            _fetchService = fetchService;
            _costService = costService;
        }

        public async Task<FetchNdviHistoryResponse?> Handle(FetchNdviHistoryRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            // 1) Posse: a área tem de ser do chamador.
            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.AreaId && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null)
            {
                _notifier.Handle(new Notification("Área não encontrada."));
                return null;
            }

            // 2) Data no passado: não há passagem no futuro para buscar.
            if (request.Date.Date >= DateTime.UtcNow.Date)
            {
                _notifier.Handle(new Notification("Escolha uma data no passado para o histórico."));
                return null;
            }

            var (from, to) = NdviFetchService.HistoryWindow(request.Date);

            // 3) Curto-circuito grátis: se a janela já tem passagem armazenada, devolve sem tocar a CDSE.
            var stored = await _dbContext.NdviReadings
                .Find(r => r.AreaId == area.Id && r.UserId == userId
                           && r.AcquisitionDate >= from && r.AcquisitionDate < to)
                .ToListAsync(cancellationToken);
            if (stored.Count > 0)
            {
                var dates = stored.Select(r => r.AcquisitionDate).ToList();
                return new FetchNdviHistoryResponse
                {
                    AcquisitionDates = dates.Select(FormatDate).ToList(),
                    FetchedFromCdse = false,
                    NearestDate = FormatDate(Nearest(dates, request.Date))
                };
            }

            // 4) Teto mensal de PU: mesmo freio do worker. Só depois do curto-circuito, para não
            //    recusar uma consulta que seria grátis.
            var settings = await _dbContext.PlatformAiSettings.Find(_ => true).FirstOrDefaultAsync(cancellationToken);
            if (settings is null || !settings.Sentinel2Enabled)
            {
                _notifier.Handle(new Notification("Sensoriamento por satélite está desligado."));
                return null;
            }
            if (settings.NdviMonthlyBudgetCents > 0)
            {
                var monthCost = await _costService.GetCurrentMonthCostCentsAsync(cancellationToken);
                if (monthCost >= settings.NdviMonthlyBudgetCents)
                {
                    _notifier.Handle(new Notification(
                        "Cota mensal de satélite atingida — a busca retroativa fica indisponível até o mês virar."));
                    return null;
                }
            }

            // 5) Chamada paga à CDSE.
            var outcome = await _fetchService.FetchHistoricalAsync(area, request.Date, cancellationToken);
            if (outcome.Status != NdviFetchStatus.Success)
            {
                _notifier.Handle(new Notification(outcome.Reason ?? "Falha ao buscar o histórico na CDSE."));
                return null;
            }

            var found = outcome.AcquisitionDates ?? [];
            if (found.Count == 0)
            {
                _notifier.Handle(new Notification("Nenhuma passagem de satélite encontrada perto dessa data."));
                return null;
            }

            var parsed = found.Select(d => DateTime.Parse(d, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)).ToList();
            return new FetchNdviHistoryResponse
            {
                AcquisitionDates = found,
                FetchedFromCdse = true,
                NearestDate = FormatDate(Nearest(parsed, request.Date))
            };
        }

        private static DateTime Nearest(List<DateTime> dates, DateTime target) =>
            dates.OrderBy(d => Math.Abs((d - target).TotalDays)).First();

        private static string FormatDate(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
