using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Ndvi
{
    /// <summary>
    /// GeoTIFF de doses (prescrição para taxa variável) de uma passagem: valor do pixel = kg/ha.
    /// Posse dupla (área do dono → reading da área), verbatim do <see cref="GetNdviZonesHandler"/>.
    /// A dose é gerada da CDSE sob demanda (paga PU; <b>não</b> cacheada — depende do perfil, que
    /// muda). Null → o controller devolve 404.
    /// </summary>
    public class GetFertilizationPrescriptionTiffHandler
        : IRequestHandler<GetFertilizationPrescriptionTiffRequest, NdviOverlayImageResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;
        private readonly INdviPrescriptionRasterService _rasterService;

        public GetFertilizationPrescriptionTiffHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INotifier notifier,
            INdviPrescriptionRasterService rasterService)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
            _rasterService = rasterService;
        }

        public async Task<NdviOverlayImageResponse?> Handle(
            GetFertilizationPrescriptionTiffRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            // 1) Posse: área do chamador.
            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.AreaId && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null)
            {
                _notifier.Handle(new Notification("Área não encontrada."));
                return null;
            }

            // 2) Reading dessa área e do chamador.
            var reading = await _dbContext.NdviReadings
                .Find(r => r.Id == request.ReadingId && r.AreaId == area.Id && r.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (reading is null)
            {
                _notifier.Handle(new Notification("Passagem não encontrada."));
                return null;
            }

            // 3) Nublada renderia um raster todo-zero — não vale a chamada paga.
            if (reading.CloudRejected)
            {
                _notifier.Handle(new Notification("Passagem nublada não gera prescrição — escolha uma passagem de céu limpo."));
                return null;
            }

            // 4) Perfil: mesma regra do relatório (override / auto pela cultura).
            var profiles = await _dbContext.FertilizationProfiles.Find(_ => true).ToListAsync(cancellationToken);
            var (profile, profileError) = FertilizationProfileResolver.Resolve(profiles, area, request.ProfileId);
            if (profile is null)
            {
                _notifier.Handle(new Notification(profileError!));
                return null;
            }

            // 5) Gera o GeoTIFF na CDSE (null → 404: kill-switch/erro/raster vazio).
            var tiff = await _rasterService.GetTiffAsync(
                area, reading, profile, DoseEvalscript.Normalize(request.Nutrient), cancellationToken);
            if (tiff is null) return null;

            return new NdviOverlayImageResponse { Content = tiff, ContentType = "image/tiff" };
        }
    }
}
