using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Pivots
{
    public class GetListPivotByUserIdRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_UserId()
        {
            var request = new GetListPivotByUserIdRequest();
            request.UserId = 123;
            Assert.Equal(123, request.UserId);
        }

        [Fact]
        public void Default_UserId_Is_Null()
        {
            var request = new GetListPivotByUserIdRequest();
            Assert.Null(request.UserId);
        }
    }
}