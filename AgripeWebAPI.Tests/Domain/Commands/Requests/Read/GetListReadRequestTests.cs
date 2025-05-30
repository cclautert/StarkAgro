using AgripeWebAPI.Domain.Commands.Requests.Sensor;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Read
{
    public class GetListReadRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            // Arrange
            var request = new GetListReadRequest();

            // Act
            request.UserId = 5;
            request.NumberOfReads = 25;

            // Assert
            Assert.Equal(5, request.UserId);
            Assert.Equal(25, request.NumberOfReads);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            // Arrange & Act
            var request = new GetListReadRequest();

            // Assert
            Assert.Equal(0, request.UserId);
            Assert.Equal(10, request.NumberOfReads);
        }
    }
}