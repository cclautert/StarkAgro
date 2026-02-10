using AgripeWebAPI.Domain.Commands.Requests.Users;
using AgripeWebAPI.Domain.Commands.Responses.Users;
using AgripeWebAPI.Domain.Handlers.Users;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Handlers.Users
{
    public class GetUserHandlerTests
    {
        private agpDBContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<agpDBContext>()
                .UseInMemoryDatabase(databaseName: "TestDb_UserHandler" + System.Guid.NewGuid())
                .Options;
            return new agpDBContext(options);
        }

        [Fact]
        public async Task Handle_Returns_User_Response()
        {
            var context = CreateInMemoryContext();
            context.Users.Add(new User { Id = 2, Name = "User2", Email = "user2@example.com", Password = "pass" });
            context.SaveChanges();

            var notifier = new Mock<INotifier>();
            var logger = new Mock<ILogger<GetUserHandler>>();

            var handler = new GetUserHandler(context, notifier.Object, logger.Object);
            var request = new GetUserRequest { Id = 2 };

            var result = await handler.Handle(request, default);

            Assert.NotNull(result);
            Assert.Equal(2, result.Id);
            Assert.Equal("User2", result.Name);
            Assert.Equal("user2@example.com", result.Email);
        }
    }
}