using AgripeWebAPI.Domain.Commands.Requests.Reads;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Reads
{
    public class CreateReadRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            // Arrange
            var request = new CreateReadRequest();

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
            var request = new CreateReadRequest();

            // Assert
            Assert.Null(request.Code);
            Assert.Equal(0m, request.Value);
            Assert.Null(request.UserId);
        }

        [Fact]
        public void UserId_SetAndGet()
        {
            var request = new CreateReadRequest { UserId = 42 };
            Assert.Equal(42, request.UserId);
        }
    }
}