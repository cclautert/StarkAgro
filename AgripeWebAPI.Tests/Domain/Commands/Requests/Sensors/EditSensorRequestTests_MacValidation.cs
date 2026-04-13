using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Models.Entities;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Sensors
{
    public class EditSensorRequestTests_MacValidation
    {
        [Fact]
        public void Validate_ValidMac_NoValidationErrors()
        {
            // Arrange
            var request = new EditSensorRequest
            {
                Id = 1,
                Code = "AA:BB:CC:DD:EE:FF",
                Pivot = new Pivot { Id = 1 },
                Quadrante = 1
            };
            var ctx = new ValidationContext(request);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, ctx, results, true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(results);
        }

        [Fact]
        public void Validate_InvalidCode_ReturnsValidationError()
        {
            // Arrange
            var request = new EditSensorRequest
            {
                Id = 1,
                Code = "NOT-A-MAC",
                Pivot = new Pivot { Id = 1 },
                Quadrante = 1
            };
            var ctx = new ValidationContext(request);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, ctx, results, true);

            // Assert
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage != null && r.ErrorMessage.Contains("XX:XX:XX:XX:XX:XX"));
        }

        [Fact]
        public void Validate_NullCode_IsAllowed_WhenNotRequired()
        {
            // Arrange — Code is nullable on EditSensorRequest and [MacAddress] is applied without [Required]
            var request = new EditSensorRequest
            {
                Id = 1,
                Code = null,
                Pivot = new Pivot { Id = 1 },
                Quadrante = 1
            };
            var ctx = new ValidationContext(request);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, ctx, results, true);

            // Assert — null is valid because [Required] is absent (partial update design)
            Assert.True(isValid);
            Assert.Empty(results);
        }
    }
}
