using AgripeWebAPI.Domain.Commands.Requests.Sensors;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Sensor
{
    public class CreateSensorRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            // Arrange
            var request = new CreateSensorRequest();

            // Act
            request.UserId = 2;
            request.Code = "SENSOR-001";
            request.Quadrante = 3;

            // Assert
            Assert.Equal(2, request.UserId);
            Assert.Equal("SENSOR-001", request.Code);
            Assert.Equal(3, request.Quadrante);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            // Arrange & Act
            var request = new CreateSensorRequest();

            // Assert
            Assert.Null(request.UserId);
            Assert.Null(request.Code);
            Assert.Equal(0, request.Quadrante);
        }
    }
}