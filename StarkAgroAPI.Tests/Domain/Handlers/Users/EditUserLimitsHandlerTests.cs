using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Handlers.Users;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Users
{
    public class EditUserLimitsHandlerTests
    {
        [Fact]
        public void Constructor_NullDbContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EditUserLimitsHandler(null!));
        }

        [Fact]
        public async Task Handle_Updates_UserLimits_And_Returns_Response()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();

            var user = new User { Id = 1, Name = "Alice", Email = "alice@example.com" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserLimitsHandler(mockDbContext.Object);
            var result = await handler.Handle(
                new EditUserLimitsRequest { Id = 1, LimiteInferior = 15m, LimiteSuperior = 85m }, default);

            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Alice", result.Name);
            Assert.Equal("alice@example.com", result.Email);
            Assert.Equal(15m, user.LimiteInferior);
            Assert.Equal(85m, user.LimiteSuperior);
        }

        [Fact]
        public async Task Handle_Throws_When_User_Not_Found()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();

            MongoMockHelper.SetupFind<User>(mockUsers, null);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserLimitsHandler(mockDbContext.Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                handler.Handle(new EditUserLimitsRequest { Id = 999 }, default));
        }

        [Fact]
        public async Task Handle_PersistsRainThresholdMm_WhenProvided()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();

            var user = new User { Id = 1, Name = "Alice", Email = "alice@example.com" };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserLimitsHandler(mockDbContext.Object);
            await handler.Handle(
                new EditUserLimitsRequest { Id = 1, LimiteInferior = 25m, LimiteSuperior = 75m, RainThresholdMm = 10.0 }, default);

            Assert.Equal(10.0, user.RainThresholdMm);
        }

        [Fact]
        public async Task Handle_ClearsRainThresholdMm_WhenNull()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();

            var user = new User { Id = 1, Name = "Alice", Email = "alice@example.com", RainThresholdMm = 10.0 };
            MongoMockHelper.SetupFind(mockUsers, user);
            mockUsers.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<User>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new EditUserLimitsHandler(mockDbContext.Object);
            await handler.Handle(
                new EditUserLimitsRequest { Id = 1, LimiteInferior = 25m, LimiteSuperior = 75m, RainThresholdMm = null }, default);

            Assert.Null(user.RainThresholdMm);
        }
    }
}
