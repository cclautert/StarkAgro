using StarkAgroAPI.Domain.Commands.Requests.Sensors;
using StarkAgroAPI.Models.Entities;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Sensors
{
    public class EditSensorRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            // Arrange
            var pivot = new Pivot { Id = 7 }; // Fully qualify the Pivot type to resolve ambiguity
            var request = new EditSensorRequest();

            // Act
            request.Id = 10;
            request.Pivot = pivot;
            request.UserId = 5;
            request.Code = "SENSOR-XYZ";
            request.Quadrante = 3;

            // Assert
            Assert.Equal(10, request.Id);
            Assert.Equal(pivot, request.Pivot);
            Assert.Equal(5, request.UserId);
            Assert.Equal("SENSOR-XYZ", request.Code);
            Assert.Equal(3, request.Quadrante);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            // Arrange & Act
            var request = new EditSensorRequest();

            // Assert
            Assert.Equal(0, request.Id);
            Assert.Null(request.Pivot);
            Assert.Null(request.UserId);
            Assert.Null(request.Code);
            Assert.Equal(0, request.Quadrante);
        }
    }
}