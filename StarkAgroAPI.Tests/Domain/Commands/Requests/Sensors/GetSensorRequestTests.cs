using StarkAgroAPI.Domain.Commands.Requests.Sensors;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Sensors
{
    public class GetSensorRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Code()
        {
            // Arrange
            var request = new GetSensorRequest();

            // Act
            request.Id = 1;

            // Assert
            Assert.Equal(1, request.Id);
        }

        [Fact]
        public void Default_Code_Is_Null()
        {
            // Arrange & Act
            var request = new GetSensorRequest();

            // Assert
            Assert.Equal(0, request.Id);
        }
    }
}