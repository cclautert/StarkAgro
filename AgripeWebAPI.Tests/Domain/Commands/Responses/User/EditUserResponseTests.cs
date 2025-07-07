using AgripeWebAPI.Domain.Commands.Responses.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.User
{
    public class EditUserResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var response = new EditUserResponse();
            response.Id = 2;
            response.Name = "Edit";
            response.Email = "edit@example.com";
            Assert.Equal(2, response.Id);
            Assert.Equal("Edit", response.Name);
            Assert.Equal("edit@example.com", response.Email);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var response = new EditUserResponse();
            Assert.Equal(0, response.Id);
            Assert.Null(response.Name);
            Assert.Null(response.Email);
        }
    }
}