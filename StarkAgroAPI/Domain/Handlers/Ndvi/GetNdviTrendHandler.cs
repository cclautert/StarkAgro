using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Ndvi
{
    public class GetNdviTrendHandler : IRequestHandler<GetNdviTrendRequest, NdviTrendResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public GetNdviTrendHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<NdviTrendResponse?> Handle(GetNdviTrendRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            // Ownership: a área precisa ser do chamador antes de devolver qualquer reading.
            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.AreaId && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null)
            {
                _notifier.Handle(new Notification("Área não encontrada."));
                return null;
            }

            var readings = await _dbContext.NdviReadings
                .Find(r => r.AreaId == request.AreaId && r.UserId == userId)
                .SortBy(r => r.AcquisitionDate)
                .ToListAsync(cancellationToken);

            // Série de radar (S1): datas próprias, filtrada pelo mesmo tenant.
            var radar = await _dbContext.Sentinel1Readings
                .Find(r => r.AreaId == request.AreaId && r.UserId == userId)
                .SortBy(r => r.AcquisitionDate)
                .ToListAsync(cancellationToken);

            var bbox = area.Geometry is not null
                ? Services.Ndvi.CdseProcessService.ComputeBbox(area.Geometry).ToArray()
                : null;

            return new NdviTrendResponse
            {
                AreaId = request.AreaId,
                Radar = radar.Select(r => new Sentinel1TrendPoint
                {
                    AcquisitionDate = r.AcquisitionDate,
                    RviMean = r.RviMean,
                    VvMean = r.VvMean,
                    VhMean = r.VhMean
                }).ToList(),
                Points = readings.Select(r => new NdviTrendPoint
                {
                    ReadingId = r.Id,
                    AcquisitionDate = r.AcquisitionDate,
                    NdviMean = r.NdviMean,
                    NdviMin = r.NdviMin,
                    NdviMax = r.NdviMax,
                    NdreMean = r.NdreMean,
                    NdmiMean = r.NdmiMean,
                    CloudCoveragePct = r.CloudCoveragePct,
                    CloudRejected = r.CloudRejected,
                    Classes = BuildClasses(r.ClassCounts),
                    // Só aponta overlay quando o PNG realmente existe; o bbox é o da área.
                    OverlayReadingId = r.OverlayImageFileId.HasValue ? r.Id : null,
                    Bbox = r.OverlayImageFileId.HasValue ? bbox : null
                }).ToList()
            };
        }

        /// <summary>
        /// Contagens gravadas → fatias com rótulo, cor e percentual. O casamento é <b>por chave</b>
        /// (não por índice), então leitura antiga com classe desconhecida é ignorada em vez de
        /// virar outra classe. Ordem de saída é sempre a de <c>NdviClassification.Classes</c>.
        /// </summary>
        private static List<NdviClassShare> BuildClasses(List<NdviClassCount>? counts)
        {
            if (counts is null || counts.Count == 0) return [];

            var ordered = NdviClassification.Classes
                .Select(c => (Class: c, Count: counts.FirstOrDefault(x => x.Key == c.Key)?.PixelCount ?? 0))
                .ToList();

            var percentages = NdviClassification.ToPercentages([.. ordered.Select(o => o.Count)]);

            return [.. ordered.Select((o, i) => new NdviClassShare
            {
                Key = o.Class.Key,
                Label = o.Class.Label,
                Color = o.Class.HexColor,
                MinNdvi = o.Class.LowEdge,
                MaxNdvi = o.Class.HighEdge,
                PixelCount = o.Count,
                Percent = Math.Round(percentages[i], 2)
            })];
        }
    }
}
