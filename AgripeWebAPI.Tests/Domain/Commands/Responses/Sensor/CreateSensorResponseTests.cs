using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Sensor
{
    public class CreateSensorResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            // Arrange
            var response = new CreateSensorResponse();

            // Act
            response.Id = 42;

            // Assert
            Assert.Equal(42, response.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            // Arrange & Act
            var response = new CreateSensorResponse();

            // Assert
            Assert.Equal(0, response.Id);
        }
    }
}