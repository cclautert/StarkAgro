using StarkAgroAPI.Domain.Commands.Responses.Users;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Responses.User
{
    public class DeleteUserResponseTests
    {
        [Fact]
        public void Can_Instantiate()
        {
            var response = new DeleteUserResponse();
            Assert.NotNull(response);
        }
    }
}