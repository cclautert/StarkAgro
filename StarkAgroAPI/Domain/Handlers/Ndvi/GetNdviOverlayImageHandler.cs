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
    /// Serve o PNG de overlay NDVI de uma passagem <b>só para o dono</b>: dupla checagem de posse
    /// (área do chamador → reading da área), verbatim do <see cref="GetNdviZonesHandler"/>. O PNG é
    /// <b>gerado sob demanda e cacheado</b> por <see cref="INdviOverlayImageService"/> — abrir uma
    /// passagem histórica de céu limpo rende o mapa na hora (o fetch só gera para a passagem mais
    /// nova de cada ciclo). Null → o controller devolve 404 (nublada / kill-switch / sem passagem).
    /// </summary>
    public class GetNdviOverlayImageHandler
        : IRequestHandler<GetNdviOverlayImageRequest, NdviOverlayImageResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INdviOverlayImageService _overlayImageService;

        public GetNdviOverlayImageHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INdviOverlayImageService overlayImageService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _overlayImageService = overlayImageService ?? throw new ArgumentNullException(nameof(overlayImageService));
        }

        public async Task<NdviOverlayImageResponse?> Handle(
            GetNdviOverlayImageRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to read an NDVI overlay.");

            // 1) A área tem de ser do chamador.
            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.AreaId && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null) return null;

            // 2) O reading tem de ser dessa área e do chamador.
            var reading = await _dbContext.NdviReadings
                .Find(r => r.Id == request.ReadingId && r.AreaId == area.Id && r.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (reading is null) return null;

            // 3) Serve do cache ou gera na hora (nublada/kill-switch/PNG vazio → null → 404).
            var content = await _overlayImageService.GetOrCreateOverlayAsync(area, reading, cancellationToken);
            if (content is null) return null;

            return new NdviOverlayImageResponse { Content = content, ContentType = "image/png" };
        }
    }
}
