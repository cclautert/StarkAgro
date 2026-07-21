using MongoDB.Bson;

namespace StarkAgroAPI.Models.Interfaces
{
    /// <summary>
    /// Armazenamento dos PNGs de overlay NDVI. Bucket GridFS <b>separado</b> de
    /// <see cref="IDiagnosisImageStore"/> — o caminho das fotos de laudo fica intocado.
    /// Existe para que handler/serviço não dependam do GridFS direto (testável com mock).
    /// </summary>
    public interface INdviOverlayStore
    {
        Task<ObjectId> UploadAsync(byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default);

        Task<byte[]?> DownloadAsync(ObjectId fileId, CancellationToken cancellationToken = default);

        Task DeleteAsync(ObjectId fileId, CancellationToken cancellationToken = default);
    }
}
