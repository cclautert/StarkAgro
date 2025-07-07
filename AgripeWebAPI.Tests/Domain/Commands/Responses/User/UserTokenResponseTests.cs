using AgripeWebAPI.Domain.Commands.Responses.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.User
{
    public class UserTokenResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var response = new UserTokenResponse();
            response.Token = "jwt-token";
            response.Expiration = DateTime.UtcNow.AddHours(1);

            Assert.Equal("jwt-token", response.Token);
            Assert.True(response.Expiration > DateTime.UtcNow);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var response = new UserTokenResponse();
            Assert.Null(response.Token);
            Assert.Equal(default, response.Expiration);
        }
    }
}