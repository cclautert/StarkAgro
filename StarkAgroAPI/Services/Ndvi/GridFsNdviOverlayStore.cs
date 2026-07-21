using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;

namespace StarkAgroAPI.Services.Ndvi
{
    /// <summary>
    /// GridFS no bucket <c>ndvi_overlays</c> — cópia paralela de
    /// <c>GridFsDiagnosisImageStore</c>, para não tocar no caminho de laudo que já funciona.
    /// </summary>
    public class GridFsNdviOverlayStore : INdviOverlayStore
    {
        private readonly agpDBContext _dbContext;

        public GridFsNdviOverlayStore(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<ObjectId> UploadAsync(
            byte[] content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var options = new GridFSUploadOptions
            {
                Metadata = new BsonDocument { { "contentType", contentType } }
            };

            return await _dbContext.NdviOverlays.UploadFromBytesAsync(
                fileName, content, options, cancellationToken);
        }

        public async Task<byte[]?> DownloadAsync(ObjectId fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbContext.NdviOverlays.DownloadAsBytesAsync(
                    fileId, cancellationToken: cancellationToken);
            }
            catch (GridFSFileNotFoundException)
            {
                return null;
            }
        }

        public async Task DeleteAsync(ObjectId fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _dbContext.NdviOverlays.DeleteAsync(fileId, cancellationToken);
            }
            catch (GridFSFileNotFoundException)
            {
                // Já não existe — apagar é idempotente.
            }
        }
    }
}
