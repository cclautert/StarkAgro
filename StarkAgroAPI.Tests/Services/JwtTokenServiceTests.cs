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
        public async Task GenerateTokenAsync_EmitsDerivedBooleanRoleClaims()
        {
            var service = new JwtTokenService(CreateValidConfig());
            var user = CreateTestUser();
            user.SetRole(UserRole.Admin, true);
            user.SetRole(UserRole.ResellerManager, true);

            var token = await service.GenerateTokenAsync(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal("true", jwt.Claims.First(c => c.Type == "isAdmin").Value);
            Assert.Equal("false", jwt.Claims.First(c => c.Type == "isAgronomist").Value);
            Assert.Equal("true", jwt.Claims.First(c => c.Type == "isResellerManager").Value);
        }

        [Fact]
        public async Task GenerateTokenAsync_EmitsOneRoleClaimPerRole()
        {
            var service = new JwtTokenService(CreateValidConfig());
            var user = CreateTestUser();
            user.SetRole(UserRole.Admin, true);
            user.SetRole(UserRole.Agronomist, true);

            var token = await service.GenerateTokenAsync(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var roles = jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
            Assert.Contains(UserRole.Admin, roles);
            Assert.Contains(UserRole.Agronomist, roles);
            Assert.Equal(2, roles.Count);
        }

        [Fact]
        public async Task GenerateTokenAsync_NoRoles_EmitsFalseBooleanClaimsAndNoRoleClaim()
        {
            var service = new JwtTokenService(CreateValidConfig());
            var user = CreateTestUser();

            var token = await service.GenerateTokenAsync(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal("false", jwt.Claims.First(c => c.Type == "isAdmin").Value);
            Assert.Equal("false", jwt.Claims.First(c => c.Type == "isAgronomist").Value);
            Assert.Equal("false", jwt.Claims.First(c => c.Type == "isResellerManager").Value);
            Assert.DoesNotContain(jwt.Claims, c => c.Type == "role");
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
