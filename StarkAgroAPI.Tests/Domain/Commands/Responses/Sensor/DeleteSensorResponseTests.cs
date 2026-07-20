using StarkAgroAPI.Domain.Commands.Responses.Sensors;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Responses.Sensor
{
    public class DeleteSensorResponseTests
    {
        [Fact]
        public void Can_Instantiate()
        {
            var response = new DeleteSensorResponse();
            Assert.NotNull(response);
        }
    }
}