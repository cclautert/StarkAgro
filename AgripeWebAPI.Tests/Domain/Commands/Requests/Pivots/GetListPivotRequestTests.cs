using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Pivots
{
    public class GetListPivotRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            var request = new GetListPivotRequest();
            request.Id = 42;
            Assert.Equal(42, request.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var request = new GetListPivotRequest();
            Assert.Equal(0, request.Id);
        }
    }
}