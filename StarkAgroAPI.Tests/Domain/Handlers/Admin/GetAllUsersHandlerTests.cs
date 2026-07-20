using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Admin
{
    public class GetAllUsersHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_All_Users()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();

            var users = new List<User>
            {
                new User { Id = 1, Name = "Alice", Email = "alice@example.com", Active = true, IsAdmin = true },
                new User { Id = 2, Name = "Bob",   Email = "bob@example.com",   Active = false }
            };
            MongoMockHelper.SetupFindList(mockUsers, users);
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new GetAllUsersHandler(mockDbContext.Object);
            var result = await handler.Handle(new GetAllUsersRequest(), default);

            Assert.Equal(2, result.Count);
            Assert.Equal("Alice", result[0].Name);
            Assert.True(result[0].IsAdmin);
            Assert.False(result[1].Active);
        }

        [Fact]
        public async Task Handle_Returns_Empty_List_When_No_Users()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockUsers = new Mock<IMongoCollection<User>>();

            MongoMockHelper.SetupFindList(mockUsers, new List<User>());
            mockDbContext.Setup(c => c.Users).Returns(mockUsers.Object);

            var handler = new GetAllUsersHandler(mockDbContext.Object);
            var result = await handler.Handle(new GetAllUsersRequest(), default);

            Assert.Empty(result);
        }
    }
}
