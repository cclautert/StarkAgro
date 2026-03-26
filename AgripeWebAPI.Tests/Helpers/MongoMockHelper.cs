using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Helpers
{
    public static class MongoMockHelper
    {
        public static Mock<IAsyncCursor<T>> CreateMockCursor<T>(List<T> items)
        {
            var cursor = new Mock<IAsyncCursor<T>>();
            var called = false;

            cursor.Setup(c => c.Current).Returns(items);
            cursor.Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    if (!called) { called = true; return items.Count > 0; }
                    return false;
                });
            cursor.Setup(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (!called) { called = true; return items.Count > 0; }
                    return false;
                });
            return cursor;
        }

        /// <summary>
        /// Setup Find().FirstOrDefaultAsync() / Find().ToListAsync() (no projection)
        /// </summary>
        public static void SetupFind<T>(Mock<IMongoCollection<T>> col, T? result) where T : class
        {
            var items = result != null ? new List<T> { result } : new List<T>();
            col.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<T>>(),
                    It.IsAny<FindOptions<T, T>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateMockCursor(items).Object);
        }

        /// <summary>
        /// Setup Find().ToListAsync() with multiple results (no projection)
        /// </summary>
        public static void SetupFindList<T>(Mock<IMongoCollection<T>> col, List<T> items) where T : class
        {
            col.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<T>>(),
                    It.IsAny<FindOptions<T, T>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateMockCursor(items).Object);
        }

        /// <summary>
        /// Setup Find().Project().FirstOrDefaultAsync() / ToListAsync() with projection
        /// </summary>
        public static void SetupFindProjection<TDoc, TProj>(Mock<IMongoCollection<TDoc>> col, List<TProj> items)
            where TDoc : class where TProj : class
        {
            col.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<TDoc>>(),
                    It.IsAny<FindOptions<TDoc, TProj>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateMockCursor(items).Object);
        }

        /// <summary>
        /// Setup DeleteOneAsync returning a given count
        /// </summary>
        public static void SetupDeleteOne<T>(Mock<IMongoCollection<T>> col, long deletedCount)
        {
            col.Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<T>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(deletedCount));
        }

        /// <summary>
        /// Setup DeleteManyAsync
        /// </summary>
        public static void SetupDeleteMany<T>(Mock<IMongoCollection<T>> col, long deletedCount = 0)
        {
            col.Setup(c => c.DeleteManyAsync(
                    It.IsAny<FilterDefinition<T>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(deletedCount));
        }
    }
}
