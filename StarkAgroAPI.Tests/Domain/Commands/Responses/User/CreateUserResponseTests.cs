using StarkAgroAPI.Domain.Commands.Responses.Users;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Responses.User
{
    public class CreateUserResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var response = new CreateUserResponse();
            response.Id = 1;
            response.Name = "User";
            response.Email = "user@example.com";
            Assert.Equal(1, response.Id);
            Assert.Equal("User", response.Name);
            Assert.Equal("user@example.com", response.Email);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var response = new CreateUserResponse();
            Assert.Equal(0, response.Id);
            Assert.Null(response.Name);
            Assert.Null(response.Email);
        }
    }
}