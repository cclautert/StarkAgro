using AgripeWebAPI.Domain.Commands.Requests.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Users
{
    public class UserTokenRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var request = new UserTokenRequest();
            request.Email = "user@example.com";
            request.Password = "securePassword";
            Assert.Equal("user@example.com", request.Email);
            Assert.Equal("securePassword", request.Password);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var request = new UserTokenRequest();
            Assert.Null(request.Email);
            Assert.Null(request.Password);
        }
    }
}