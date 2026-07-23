using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.Ndvi;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Ndvi
{
    /// <summary>
    /// Serve o GeoTIFF de zonas de uma passagem <b>só para o dono</b>: dupla checagem de posse
    /// (área do chamador → reading da área), verbatim do <see cref="GetNdviOverlayImageHandler"/>.
    /// O TIFF é gerado sob demanda e cacheado por <see cref="INdviZoneService"/>. Null → 404.
    /// </summary>
    public class GetNdviZonesHandler : IRequestHandler<GetNdviZonesRequest, NdviOverlayImageResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INdviZoneService _zoneService;

        public GetNdviZonesHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INdviZoneService zoneService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _zoneService = zoneService ?? throw new ArgumentNullException(nameof(zoneService));
        }

        public async Task<NdviOverlayImageResponse?> Handle(
            GetNdviZonesRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to download NDVI zones.");

            // 1) A área tem de ser do chamador.
            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.AreaId && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null) return null;

            // 2) O reading tem de ser dessa área e do chamador. O zoneamento só faz sentido para uma
            //    passagem com pixel válido — a mesma que tem overlay (nublada não rende raster).
            var reading = await _dbContext.NdviReadings
                .Find(r => r.Id == request.ReadingId && r.AreaId == area.Id && r.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (reading is null || reading.OverlayImageFileId is null) return null;

            var tiff = await _zoneService.GetOrCreateTiffAsync(area, reading, cancellationToken);
            if (tiff is null) return null;

            return new NdviOverlayImageResponse { Content = tiff, ContentType = "image/tiff" };
        }
    }
}
