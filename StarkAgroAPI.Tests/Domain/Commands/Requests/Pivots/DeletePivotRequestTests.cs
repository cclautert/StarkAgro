using StarkAgroAPI.Domain.Commands.Requests.Pivots;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Requests.Pivots
{
    public class DeletePivotRequestTests
    {
        [Fact]
        public void Can_Set_And_Get_Id()
        {
            var request = new DeletePivotRequest();
            request.Id = 99;
            Assert.Equal(99, request.Id);
        }

        [Fact]
        public void Default_Id_Is_Zero()
        {
            var request = new DeletePivotRequest();
            Assert.Equal(0, request.Id);
        }
    }
}