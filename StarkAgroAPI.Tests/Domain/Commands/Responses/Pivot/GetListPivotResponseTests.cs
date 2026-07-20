using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Responses.Pivot
{
    public class GetListPivotResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var response = new GetListPivotResponse();
            response.Id = 123;
            response.Name = "PivotName";
            Assert.Equal(123, response.Id);
            Assert.Equal("PivotName", response.Name);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var response = new GetListPivotResponse();
            Assert.Equal(0, response.Id);
            Assert.Null(response.Name);
        }
    }
}