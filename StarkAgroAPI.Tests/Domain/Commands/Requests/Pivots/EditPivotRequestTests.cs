using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Pivots
{
    public class EditPivotRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var request = new EditPivotRequest();
            request.Id = 42;
            request.Name = "Edit Pivot";
            Assert.Equal(42, request.Id);
            Assert.Equal("Edit Pivot", request.Name);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var request = new EditPivotRequest();
            Assert.Null(request.Id);
            Assert.Null(request.Name);
        }
    }
}