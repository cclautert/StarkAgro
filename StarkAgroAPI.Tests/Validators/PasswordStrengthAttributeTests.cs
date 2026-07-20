using StarkAgroAPI.Validators;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace StarkAgroAPI.Tests.Validators
{
    public class PasswordStrengthAttributeTests
    {
        private readonly PasswordStrengthAttribute _attribute;

        public PasswordStrengthAttributeTests()
        {
            _attribute = new PasswordStrengthAttribute();
        }

        [Theory]
        [InlineData("ValidPass1!")]
        [InlineData("StrongP@ssw0rd")]
        [InlineData("Test1234#")]
        [InlineData("MyP@ssw0rd")]
        public void IsValid_ShouldReturnTrueForValidPasswords(string password)
        {
            // Act
            var result = _attribute.IsValid(password);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("short1!")] // Too short
        [InlineData("NOLOWERCASE1!")] // No lowercase
        [InlineData("nouppercase1!")] // No uppercase
        [InlineData("NoNumbers!")] // No numbers
        [InlineData("NoSpecial123")] // No special characters
        [InlineData("")]
        [InlineData(null)]
        public void IsValid_ShouldReturnFalseForInvalidPasswords(string? password)
        {
            // Act
            var result = _attribute.IsValid(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void FormatErrorMessage_ShouldReturnFormattedMessage()
        {
            // Act
            var message = _attribute.FormatErrorMessage("Password");

            // Assert
            Assert.Contains("Password", message);
            Assert.Contains("8", message);
            Assert.Contains("100", message);
        }
    }
}
