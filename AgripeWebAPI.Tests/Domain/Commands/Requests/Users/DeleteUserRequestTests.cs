using AgripeWebAPI.Domain.Commands.Requests.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Users
{
    public class DeleteUserRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            var request = new DeleteUserRequest();
            request.Id = 77;
            Assert.Equal(77, request.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var request = new DeleteUserRequest();
            Assert.Equal(0, request.Id);
        }
    }
}