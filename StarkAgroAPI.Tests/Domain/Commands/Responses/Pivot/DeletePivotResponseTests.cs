using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using Xunit;

namespace StarkAgroAPI.Tests.Domain.Commands.Responses.Pivot
{
    public class DeletePivotResponseTests
    {
        [Fact]
        public void Can_Instantiate()
        {
            var response = new DeletePivotResponse();
            Assert.NotNull(response);
        }
    }
}