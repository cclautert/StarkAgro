using AgripeWebAPI.Domain.Commands.Responses.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.User
{
    public class GetUserResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var response = new GetUserResponse();
            response.Id = 10;
            response.Name = "Test User";
            response.Email = "test@example.com";
            Assert.Equal(10, response.Id);
            Assert.Equal("Test User", response.Name);
            Assert.Equal("test@example.com", response.Email);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var response = new GetUserResponse();
            Assert.Equal(0, response.Id);
            Assert.Null(response.Name);
            Assert.Null(response.Email);
        }
    }
}