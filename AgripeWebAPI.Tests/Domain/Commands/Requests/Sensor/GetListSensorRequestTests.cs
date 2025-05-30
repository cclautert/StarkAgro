using AgripeWebAPI.Domain.Commands.Requests.Sensor;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Sensor
{
    public class GetListSensorRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_PivotId()
        {
            // Arrange
            var request = new GetListSensorRequest();

            // Act
            request.PivotId = 42;

            // Assert
            Assert.Equal(42, request.PivotId);
        }

        [Fact]
        public void Default_PivotId_Is_Zero()
        {
            // Arrange & Act
            var request = new GetListSensorRequest();

            // Assert
            Assert.Equal(0, request.PivotId);
        }
    }
}