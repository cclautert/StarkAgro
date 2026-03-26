using AgripeWebAPI.Domain.Commands.Requests.Users;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Users
{
    public class ExternalLoginRequestTests
    {
        [Fact]
        public void Properties_SetAndGet()
        {
            // Arrange
            var request = new ExternalLoginRequest
            {
                Provider = "Google",
                Code = "auth-code-123",
                RedirectUri = "http://localhost:4200/login/callback"
            };

            // Act & Assert
            Assert.Equal("Google", request.Provider);
            Assert.Equal("auth-code-123", request.Code);
            Assert.Equal("http://localhost:4200/login/callback", request.RedirectUri);
        }

        [Fact]
        public void DefaultValues_AreEmpty()
        {
            // Arrange
            var request = new ExternalLoginRequest();

            // Act & Assert
            Assert.Equal(string.Empty, request.Provider);
            Assert.Equal(string.Empty, request.Code);
            Assert.Equal(string.Empty, request.RedirectUri);
        }
    }
}
