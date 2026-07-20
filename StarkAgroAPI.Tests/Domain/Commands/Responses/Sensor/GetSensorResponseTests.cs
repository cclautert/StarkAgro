using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Responses.Sensor
{
    public class GetSensorResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            // Arrange
            var response = new GetSensorResponse();

            // Act
            response.Id = 1;
            response.Quadrante = 3;
            response.Code = "CODE-123";

            // Assert
            Assert.Equal(1, response.Id);
            Assert.Equal(3, response.Quadrante);
            Assert.Equal("CODE-123", response.Code);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            // Arrange & Act
            var response = new GetSensorResponse();

            // Assert
            Assert.Equal(0, response.Id);
            Assert.Equal(0, response.Quadrante);
            Assert.Null(response.Code);
        }
    }
}