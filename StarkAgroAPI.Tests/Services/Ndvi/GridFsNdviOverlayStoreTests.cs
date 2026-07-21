using StarkAgroAPI.Models;
using StarkAgroAPI.Services.Ndvi;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using Moq;

namespace StarkAgroAPI.Tests.Services.Ndvi
{
    public class GridFsNdviOverlayStoreTests
    {
        private static (GridFsNdviOverlayStore store, Mock<IGridFSBucket> bucket) Build()
        {
            var bucket = new Mock<IGridFSBucket>();
            var db = new Mock<agpDBContext>();
            db.Setup(d => d.NdviOverlays).Returns(bucket.Object);
            return (new GridFsNdviOverlayStore(db.Object), bucket);
        }

        [Fact]
        public async Task Upload_DelegatesToBucket_ReturnsFileId()
        {
            var (store, bucket) = Build();
            var id = ObjectId.GenerateNewId();
            bucket.Setup(b => b.UploadFromBytesAsync(
                    It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<GridFSUploadOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(id);

            var result = await store.UploadAsync([1, 2, 3], "ndvi.png", "image/png");

            Assert.Equal(id, result);
        }

        [Fact]
        public async Task Download_Existing_ReturnsBytes()
        {
            var (store, bucket) = Build();
            var id = ObjectId.GenerateNewId();
            bucket.Setup(b => b.DownloadAsBytesAsync(id, It.IsAny<GridFSDownloadOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([9, 9, 9]);

            var bytes = await store.DownloadAsync(id);

            Assert.Equal([9, 9, 9], bytes);
        }

        [Fact]
        public async Task Download_NotFound_ReturnsNull()
        {
            var (store, bucket) = Build();
            var id = ObjectId.GenerateNewId();
            bucket.Setup(b => b.DownloadAsBytesAsync(id, It.IsAny<GridFSDownloadOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new GridFSFileNotFoundException(id));

            Assert.Null(await store.DownloadAsync(id));
        }

        [Fact]
        public async Task Delete_DelegatesToBucket()
        {
            var (store, bucket) = Build();
            var id = ObjectId.GenerateNewId();
            bucket.Setup(b => b.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            await store.DeleteAsync(id);

            bucket.Verify(b => b.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Delete_NotFound_IsIdempotent()
        {
            var (store, bucket) = Build();
            var id = ObjectId.GenerateNewId();
            bucket.Setup(b => b.DeleteAsync(id, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new GridFSFileNotFoundException(id));

            await store.DeleteAsync(id); // não lança
        }
    }
}
