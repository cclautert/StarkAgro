using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using System;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Read
{
    public class GetReadResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            // Arrange
            var response = new GetReadResponse();
            var now = DateTime.UtcNow;

            // Act
            response.Id = 1;
            response.SensorId = 2;
            response.Value = 123.45m;
            response.Date = now;

            // Assert
            Assert.Equal(1, response.Id);
            Assert.Equal(2, response.SensorId);
            Assert.Equal(123.45m, response.Value);
            Assert.Equal(now, response.Date);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            // Arrange & Act
            var response = new GetReadResponse();

            // Assert
            Assert.Equal(0, response.Id);
            Assert.Equal(0, response.SensorId);
            Assert.Equal(0m, response.Value);
            Assert.Equal(default(DateTime), response.Date);
        }
    }
}