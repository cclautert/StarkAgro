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
            request.Id = 1;
            Assert.Equal(1, request.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var request = new GetUserRequest();
            Assert.Equal(0, request.Id);
        }
    }
}