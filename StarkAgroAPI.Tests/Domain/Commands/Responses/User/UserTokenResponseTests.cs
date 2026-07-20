using StarkAgroAPI.Domain.Commands.Responses.Users;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Responses.User
{
    public class UserTokenResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var response = new UserTokenResponse();
            response.Token = "jwt-token";

            Assert.Equal("jwt-token", response.Token);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var response = new UserTokenResponse();
            Assert.Null(response.Token);
        }
    }
}