using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Pivots
{
    public class GetPivotRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            var request = new GetPivotRequest();
            request.Id = 55;
            Assert.Equal(55, request.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var request = new GetPivotRequest();
            Assert.Equal(0, request.Id);
        }
    }
}