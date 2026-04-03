using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using Xunit;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Pivot
{
    public class GetPivotResponseTests
    {
        [Fact]
        public void Can_Set_And_Get_Properties()
        {
            var response = new GetPivotResponse();
            response.Id = 456;
            response.Name = "PivotTest";
            Assert.Equal(456, response.Id);
            Assert.Equal("PivotTest", response.Name);
        }

        [Fact]
        public void Default_Values_Are_Correct()
        {
            var response = new GetPivotResponse();
            Assert.Equal(0, response.Id);
            Assert.Null(response.Name);
        }

        [Fact]
        public void LimiteInferior_LimiteSuperior_SetAndGet()
        {
            var response = new GetPivotResponse { LimiteInferior = 15.5m, LimiteSuperior = 85.0m };
            Assert.Equal(15.5m, response.LimiteInferior);
            Assert.Equal(85.0m, response.LimiteSuperior);
        }
    }
}