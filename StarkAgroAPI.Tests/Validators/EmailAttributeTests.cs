using StarkAgroAPI.Validators;
using Xunit;

namespace StarkAgroAPI.Tests.Validators
{
    public class EmailAttributeTests
    {
        private readonly EmailAttribute _attribute;

        public EmailAttributeTests()
        {
            _attribute = new EmailAttribute();
        }

        [Theory]
        [InlineData("test@example.com")]
        [InlineData("user.name@example.com")]
        [InlineData("user+tag@example.co.uk")]
        [InlineData("user123@example-domain.com")]
        public void IsValid_ShouldReturnTrueForValidEmails(string email)
        {
            // Act
            var result = _attribute.IsValid(email);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("invalid@")]
        [InlineData("@invalid.com")]
        [InlineData("invalid@.com")]
        [InlineData("invalid@com")]
        [InlineData("")]
        [InlineData(null)]
        public void IsValid_ShouldReturnFalseForInvalidEmails(string? email)
        {
            // Act
            var result = _attribute.IsValid(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValid_ShouldReturnFalseForEmailExceedingMaxLength()
        {
            // Arrange
            var longEmail = "a@" + new string('a', 250) + ".com"; // Exceeds 254 characters

            // Act
            var result = _attribute.IsValid(longEmail);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void FormatErrorMessage_ShouldReturnFormattedMessage()
        {
            // Act
            var message = _attribute.FormatErrorMessage("Email");

            // Assert
            Assert.Contains("Email", message);
            Assert.Contains("valid email", message);
        }
    }
}
