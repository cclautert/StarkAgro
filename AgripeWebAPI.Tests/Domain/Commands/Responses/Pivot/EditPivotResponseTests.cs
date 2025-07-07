using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Pivot
{
    public class EditPivotResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            var response = new EditPivotResponse();
            response.Id = 8;
            Assert.Equal(8, response.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var response = new EditPivotResponse();
            Assert.Equal(0, response.Id);
        }
    }
}