using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;

namespace AgripeWebAPI.Services.Diagnosis
{
    public class GridFsDiagnosisImageStore : IDiagnosisImageStore
    {
        private readonly agpDBContext _dbContext;

        public GridFsDiagnosisImageStore(agpDBContext dbContext)
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

            return await _dbContext.DiagnosisImages.UploadFromBytesAsync(
                fileName, content, options, cancellationToken);
        }

        public async Task<byte[]?> DownloadAsync(ObjectId fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbContext.DiagnosisImages.DownloadAsBytesAsync(
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
                await _dbContext.DiagnosisImages.DeleteAsync(fileId, cancellationToken);
            }
            catch (GridFSFileNotFoundException)
            {
                // Já não existe — apagar é idempotente.
            }
        }
    }
}
