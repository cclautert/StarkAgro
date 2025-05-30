using AgripeWebAPI.Domain.Commands.Responses.Sensor;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Sensor
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
            response.PivoId = 2;
            response.UserId = 3;
            response.Quadrante = 4;
            response.Code = "CODE-123";

            // Assert
            Assert.Equal(1, response.Id);
            Assert.Equal(2, response.PivoId);
            Assert.Equal(3, response.UserId);
            Assert.Equal(4, response.Quadrante);
            Assert.Equal("CODE-123", response.Code);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            // Arrange & Act
            var response = new GetSensorResponse();

            // Assert
            Assert.Equal(0, response.Id);
            Assert.Equal(0, response.PivoId);
            Assert.Equal(0, response.UserId);
            Assert.Equal(0, response.Quadrante);
            Assert.Null(response.Code);
        }
    }
}