using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Pivot
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