using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Sensor
{
    public class EditSensorResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            var response = new EditSensorResponse();
            response.Id = 99;
            Assert.Equal(99, response.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var response = new EditSensorResponse();
            Assert.Equal(0, response.Id);
        }
    }
}