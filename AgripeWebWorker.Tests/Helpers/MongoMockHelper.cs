using MongoDB.Driver;
using Moq;

namespace AgripeWebWorker.Tests.Helpers
{
    public static class MongoMockHelper
    {
        public static Mock<IAsyncCursor<T>> CreateMockCursor<T>(List<T> items)
        {
            var cursor = new Mock<IAsyncCursor<T>>();
            var hasCalledMoveNext = false;

            cursor.Setup(c => c.Current).Returns(items);
            cursor
                .Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    if (!hasCalledMoveNext)
                    {
                        hasCalledMoveNext = true;
                        return items.Count > 0;
                    }
                    return false;
                });
            cursor.Setup(c => c.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (!hasCalledMoveNext)
                    {
                        hasCalledMoveNext = true;
                        return items.Count > 0;
                    }
                    return false;
                });

            return cursor;
        }

        public static void SetupFind<T>(Mock<IMongoCollection<T>> mockCollection, T? result) where T : class
        {
            var items = result != null ? new List<T> { result } : new List<T>();

            // Mock FindAsync - this is what Find().FirstOrDefaultAsync() ultimately invokes
            // The Find() extension method creates a FindFluent that calls FindAsync on the collection
            mockCollection
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<T>>(),
                    It.IsAny<FindOptions<T, T>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateMockCursor(items).Object);
        }
    }
}
