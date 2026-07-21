using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Ndvi
{
    /// <summary>
    /// Serve o PNG de overlay NDVI do GridFS <b>só para o dono</b>: dupla checagem de posse
    /// (área do chamador → reading da área) antes de tocar o bucket, igual ao caminho de imagem
    /// de laudo em <c>GetPlantDiagnosisImageHandler</c>. Retorna null → o controller devolve 404.
    /// </summary>
    public class GetNdviOverlayImageHandler
        : IRequestHandler<GetNdviOverlayImageRequest, NdviOverlayImageResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INdviOverlayStore _overlayStore;

        public GetNdviOverlayImageHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            INdviOverlayStore overlayStore)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _overlayStore = overlayStore ?? throw new ArgumentNullException(nameof(overlayStore));
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

            // 2) O reading tem de ser dessa área e do chamador, e ter overlay gerado.
            var reading = await _dbContext.NdviReadings
                .Find(r => r.Id == request.ReadingId && r.AreaId == area.Id && r.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (reading?.OverlayImageFileId is null) return null;

            var content = await _overlayStore.DownloadAsync(reading.OverlayImageFileId.Value, cancellationToken);
            if (content is null) return null;

            return new NdviOverlayImageResponse { Content = content, ContentType = "image/png" };
        }
    }
}
