using AgripeWebAPI.Domain.Commands.Requests.Sensor;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Sensor
{
    public class GetSensorRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Code()
        {
            // Arrange
            var request = new GetSensorRequest();

            // Act
            request.Code = "ABC123";

            // Assert
            Assert.Equal("ABC123", request.Code);
        }

        [Fact]
        public void Default_Code_Is_Null()
        {
            // Arrange & Act
            var request = new GetSensorRequest();

            // Assert
            Assert.Null(request.Code);
        }
    }
}