using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Pivots
{
    public class CreatePivotRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var request = new CreatePivotRequest();
            request.UserId = 123;
            request.Name = "Test Pivot";
            Assert.Equal(123, request.UserId);
            Assert.Equal("Test Pivot", request.Name);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var request = new CreatePivotRequest();
            Assert.Null(request.UserId);
            Assert.Null(request.Name);
        }
    }
}