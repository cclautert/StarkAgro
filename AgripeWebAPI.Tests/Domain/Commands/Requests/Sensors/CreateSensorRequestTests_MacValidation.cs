using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Models.Entities;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Sensors
{
    public class CreateSensorRequestTests_MacValidation
    {
        [Fact]
        public void Validate_ValidMac_NoValidationErrors()
        {
            // Arrange
            var request = new CreateSensorRequest
            {
                Code = "5C:CF:7F:3A:54:29",
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
            var request = new CreateSensorRequest
            {
                Code = "SENSOR-001",
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
        public void Validate_NullCode_ReturnsValidationError()
        {
            // Arrange
            var request = new CreateSensorRequest
            {
                Code = null,
                Pivot = new Pivot { Id = 1 },
                Quadrante = 1
            };
            var ctx = new ValidationContext(request);
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, ctx, results, true);

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(results);
        }
    }
}
