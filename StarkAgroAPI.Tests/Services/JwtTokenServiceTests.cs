using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

namespace StarkAgroAPI.Tests.Services
{
    public class JwtTokenServiceTests
    {
        private static IConfiguration CreateValidConfig()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:secretkey", "a-secret-key-that-is-at-least-32-bytes-long!!" },
                    { "JwtSettings:Issuer", "TestIssuer" },
                    { "JwtSettings:Audience", "TestAudience" }
                }).Build();
        }

        private static User CreateTestUser()
        {
            return new User
            {
                Id = 10,
                Name = "John Doe",
                Email = "john@example.com",
                Password = "hashed",
                Active = true
            };
        }

        [Fact]
        public async Task GenerateTokenAsync_ValidUser_ReturnsToken()
        {
            // Arrange
            var service = new JwtTokenService(CreateValidConfig());

            // Act
            var token = await service.GenerateTokenAsync(CreateTestUser());

            // Assert
            Assert.False(string.IsNullOrEmpty(token));
        }

        [Fact]
        public async Task GenerateTokenAsync_IncludesIdClaim()
        {
            // Arrange
            var service = new JwtTokenService(CreateValidConfig());
            var user = CreateTestUser();

            // Act
            var token = await service.GenerateTokenAsync(user);
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            // Assert
            var idClaim = jwt.Claims.FirstOrDefault(c => c.Type == "id");
            Assert.NotNull(idClaim);
            Assert.Equal("10", idClaim.Value);
        }

        [Fact]
        public async Task GenerateTokenAsync_IncludesNameClaim()
        {
            // Arrange
            var service = new JwtTokenService(CreateValidConfig());
            var user = CreateTestUser();

            // Act
            var token = await service.GenerateTokenAsync(user);
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            // Assert
            var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == "name");
            Assert.NotNull(nameClaim);
            Assert.Equal("John Doe", nameClaim.Value);
        }

        [Fact]
        public async Task GenerateTokenAsync_IncludesEmailClaim()
        {
            // Arrange
            var service = new JwtTokenService(CreateValidConfig());
            var user = CreateTestUser();

            // Act
            var token = await service.GenerateTokenAsync(user);
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            // Assert
            var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == "email");
            Assert.NotNull(emailClaim);
            Assert.Equal("john@example.com", emailClaim.Value);
        }

        [Fact]
        public async Task GenerateTokenAsync_MissingSecretKey_Throws()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:Issuer", "TestIssuer" },
                    { "JwtSettings:Audience", "TestAudience" }
                }).Build();

            var service = new JwtTokenService(config);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GenerateTokenAsync(CreateTestUser()));
        }

        [Fact]
        public void Constructor_NullConfig_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new JwtTokenService(null!));
        }
    }
}
