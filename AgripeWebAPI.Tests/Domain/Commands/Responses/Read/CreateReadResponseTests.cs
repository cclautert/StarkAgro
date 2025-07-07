using AgripeWebAPI.Domain.Commands.Responses.Reads;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Read
{
    public class CreateReadResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            // Arrange
            var response = new CreateReadResponse();

            // Act
            response.Id = 100;

            // Assert
            Assert.Equal(100, response.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            // Arrange & Act
            var response = new CreateReadResponse();

            // Assert
            Assert.Equal(0, response.Id);
        }
    }
}