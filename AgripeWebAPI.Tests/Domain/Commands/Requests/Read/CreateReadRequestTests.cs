using AgripeWebAPI.Domain.Commands.Requests.Read;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Read
{
    public class CreateReadRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            // Arrange
            var request = new CreatePivotRequest();

            // Act
            request.Code = "SENSOR-XYZ";
            request.Value = 123.45m;

            // Assert
            Assert.Equal("SENSOR-XYZ", request.Code);
            Assert.Equal(123.45m, request.Value);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            // Arrange & Act
            var request = new CreatePivotRequest();

            // Assert
            Assert.Null(request.Code);
            Assert.Equal(0m, request.Value);
        }
    }
}