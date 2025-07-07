using AgripeWebAPI.Domain.Commands.Requests.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Users
{
    public class GetUserRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Email()
        {
            var request = new GetUserRequest();
            request.Email = "get@example.com";
            Assert.Equal("get@example.com", request.Email);
        }

        [Fact]
        public void Default_Email_Is_Null()
        {
            var request = new GetUserRequest();
            Assert.Null(request.Email);
        }
    }
}