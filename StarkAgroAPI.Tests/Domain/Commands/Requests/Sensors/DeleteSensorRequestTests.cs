using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Sensors
{
    public class DeleteSensorRequestTests
    {
        [Fact]
        public void Can_Instantiate_DeleteSensorRequest()
        {
            // Act
            var request = new DeleteSensorRequest();

            // Assert
            Assert.NotNull(request);
        }
    }
}