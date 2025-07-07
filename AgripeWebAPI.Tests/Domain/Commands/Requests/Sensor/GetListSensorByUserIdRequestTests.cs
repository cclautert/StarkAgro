using AgripeWebAPI.Domain.Commands.Requests.Sensors;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Sensor
{
    public class GetListSensorByUserIdRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_UserId()
        {
            // Arrange
            var request = new GetListSensorByUserIdRequest();

            // Act
            request.UserId = 123;

            // Assert
            Assert.Equal(123, request.UserId);
        }

        [Fact]
        public void Default_UserId_Is_Zero()
        {
            // Arrange & Act
            var request = new GetListSensorByUserIdRequest();

            // Assert
            Assert.Null(request.UserId);
        }
    }
}