using AgripeWebAPI.Validators;
using Xunit;

namespace AgripeWebAPI.Tests.Validators
{
    public class MacAddressAttributeTests
    {
        private readonly MacAddressAttribute _attribute;

        public MacAddressAttributeTests()
        {
            _attribute = new MacAddressAttribute();
        }

        [Fact]
        public void IsValid_ReturnsTrue_ForValidUppercaseMac()
        {
            // Act
            var result = _attribute.IsValid("5C:CF:7F:3A:54:29");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValid_ReturnsTrue_ForValidLowercaseMac()
        {
            // Act
            var result = _attribute.IsValid("5c:cf:7f:3a:54:29");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValid_ReturnsTrue_ForNull()
        {
            // null is valid — [Required] is responsible for rejecting null, not [MacAddress]
            // Act
            var result = _attribute.IsValid(null);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValid_ReturnsFalse_ForEmptyString()
        {
            // Act
            var result = _attribute.IsValid("");

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("5C-CF-7F-3A-54-29")]
        public void IsValid_ReturnsFalse_ForDashSeparator(string value)
        {
            // Act
            var result = _attribute.IsValid(value);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("5C:CF:7F:3A:54")]
        public void IsValid_ReturnsFalse_ForTooFewOctets(string value)
        {
            // Act
            var result = _attribute.IsValid(value);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("5C:CF:7F:3A:54:29:FF")]
        public void IsValid_ReturnsFalse_ForTooManyOctets(string value)
        {
            // Act
            var result = _attribute.IsValid(value);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("ZZ:CF:7F:3A:54:29")]
        public void IsValid_ReturnsFalse_ForNonHexChars(string value)
        {
            // Act
            var result = _attribute.IsValid(value);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("SENSOR-001")]
        public void IsValid_ReturnsFalse_ForPlainString(string value)
        {
            // Act
            var result = _attribute.IsValid(value);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void FormatErrorMessage_ContainsFieldNameAndFormat()
        {
            // Act
            var message = _attribute.FormatErrorMessage("Code");

            // Assert
            Assert.Contains("Code", message);
            Assert.Contains("XX:XX:XX:XX:XX:XX", message);
        }
    }
}
