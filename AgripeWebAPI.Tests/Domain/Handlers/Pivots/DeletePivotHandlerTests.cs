using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Handlers.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class DeletePivotHandlerTests
    {
        private const int OwnerUserId = 42;

        private static Mock<ICurrentUserContext> BuildCurrentUser(int? userId = OwnerUserId)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(u => u.UserId).Returns(userId);
            return mock;
        }

        [Fact]
        public async Task Handle_Deletes_Pivot_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();
            var mockSensors = new Mock<IMongoCollection<Sensor>>();

            MongoMockHelper.SetupDeleteOne(mockPivots, 1);
            MongoMockHelper.SetupDeleteMany(mockSensors);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);
            mockDbContext.Setup(c => c.Sensors).Returns(mockSensors.Object);

            var handler = new DeletePivotHandler(mockDbContext.Object, BuildCurrentUser().Object);
            var result = await handler.Handle(new DeletePivotRequest { Id = 3 }, default);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task Handle_Throws_When_Pivot_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            MongoMockHelper.SetupDeleteOne(mockPivots, 0);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new DeletePivotHandler(mockDbContext.Object, BuildCurrentUser().Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new DeletePivotRequest { Id = 999 }, default));
        }

        [Fact]
        public async Task Handle_DifferentTenant_ThrowsKeyNotFound()
        {
            // Tenant scoping: DeleteOne filter includes UserId so cross-tenant delete returns 0.
            var mockDbContext = new Mock<agpDBContext>();
            var mockPivots = new Mock<IMongoCollection<Pivot>>();

            MongoMockHelper.SetupDeleteOne(mockPivots, 0);
            mockDbContext.Setup(c => c.Pivots).Returns(mockPivots.Object);

            var handler = new DeletePivotHandler(mockDbContext.Object, BuildCurrentUser(999).Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new DeletePivotRequest { Id = 3 }, default));
        }

        [Fact]
        public async Task Handle_UnauthenticatedUser_Throws()
        {
            var mockDbContext = new Mock<agpDBContext>();

            var handler = new DeletePivotHandler(mockDbContext.Object, BuildCurrentUser(null).Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new DeletePivotRequest { Id = 1 }, default));
        }
    }
}
