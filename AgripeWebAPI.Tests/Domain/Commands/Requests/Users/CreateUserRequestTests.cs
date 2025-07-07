using AgripeWebAPI.Domain.Commands.Requests.Users;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Users
{
    public class CreateUserRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var request = new CreateUserRequest();
            request.Name = "Test User";
            request.Email = "test@example.com";
            request.Password = "password123";
            Assert.Equal("Test User", request.Name);
            Assert.Equal("test@example.com", request.Email);
            Assert.Equal("password123", request.Password);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var request = new CreateUserRequest();
            Assert.Null(request.Name);
            Assert.Null(request.Email);
            Assert.Null(request.Password);
        }
    }
}