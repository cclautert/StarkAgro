using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Pivot
{
    public class CreatePivotResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            var response = new CreatePivotResponse();
            response.Id = 7;
            Assert.Equal(7, response.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var response = new CreatePivotResponse();
            Assert.Equal(0, response.Id);
        }
    }
}