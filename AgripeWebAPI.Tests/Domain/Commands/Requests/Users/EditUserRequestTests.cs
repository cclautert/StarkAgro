using AgripeWebAPI.Domain.Commands.Requests.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Users
{
    public class EditUserRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var request = new EditUserRequest();
            request.Name = "Edit User";
            request.Email = "edit@example.com";
            request.Password = "editpass";
            Assert.Equal("Edit User", request.Name);
            Assert.Equal("edit@example.com", request.Email);
            Assert.Equal("editpass", request.Password);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var request = new EditUserRequest();
            Assert.Null(request.Name);
            Assert.Null(request.Email);
            Assert.Null(request.Password);
        }
    }
}