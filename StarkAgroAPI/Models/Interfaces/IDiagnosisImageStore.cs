using MongoDB.Bson;

namespace StarkAgroAPI.Models.Interfaces
{
    /// <summary>
    /// Armazenamento das fotos dos laudos. Existe para que os handlers não dependam
    /// do GridFS diretamente — assim eles continuam testáveis com um mock simples,
    /// e trocar por S3 mais adiante é substituir a implementação.
    /// </summary>
    public interface IDiagnosisImageStore
    {
        Task<ObjectId> UploadAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default);

        Task<byte[]?> DownloadAsync(ObjectId fileId, CancellationToken cancellationToken = default);

        Task DeleteAsync(ObjectId fileId, CancellationToken cancellationToken = default);
    }
}
