using AgripeWebAPI.Services;
using Xunit;

namespace AgripeWebAPI.Tests.Services
{
    public class PasswordHasherServiceTests
    {
        private readonly PasswordHasherService _passwordHasher;

        public PasswordHasherServiceTests()
        {
            _passwordHasher = new PasswordHasherService();
        }

        [Fact]
        public void HashPassword_ShouldReturnHashedPassword()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hashedPassword = _passwordHasher.HashPassword(password);

            // Assert
            Assert.NotNull(hashedPassword);
            Assert.NotEqual(password, hashedPassword);
            Assert.True(hashedPassword.Length > 0);
        }

        [Fact]
        public void HashPassword_ShouldReturnDifferentHashForSamePassword()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hash1 = _passwordHasher.HashPassword(password);
            var hash2 = _passwordHasher.HashPassword(password);

            // Assert
            Assert.NotEqual(hash1, hash2); // BCrypt uses salt, so hashes should be different
        }

        [Fact]
        public void VerifyPassword_ShouldReturnTrueForCorrectPassword()
        {
            // Arrange
            var password = "TestPassword123!";
            var hashedPassword = _passwordHasher.HashPassword(password);

            // Act
            var result = _passwordHasher.VerifyPassword(password, hashedPassword);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalseForIncorrectPassword()
        {
            // Arrange
            var password = "TestPassword123!";
            var wrongPassword = "WrongPassword123!";
            var hashedPassword = _passwordHasher.HashPassword(password);

            // Act
            var result = _passwordHasher.VerifyPassword(wrongPassword, hashedPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HashPassword_ShouldThrowExceptionForNullPassword()
        {
            // Arrange
            string? password = null;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _passwordHasher.HashPassword(password!));
        }

        [Fact]
        public void HashPassword_ShouldThrowExceptionForEmptyPassword()
        {
            // Arrange
            var password = "";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _passwordHasher.HashPassword(password));
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalseForNullPassword()
        {
            // Arrange
            var hashedPassword = _passwordHasher.HashPassword("TestPassword123!");

            // Act
            var result = _passwordHasher.VerifyPassword(null!, hashedPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalseForNullHash()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var result = _passwordHasher.VerifyPassword(password, null!);

            // Assert
            Assert.False(result);
        }
    }
}
