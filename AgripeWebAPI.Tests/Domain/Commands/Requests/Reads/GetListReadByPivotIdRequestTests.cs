using AgripeWebAPI.Domain.Commands.Requests.Reads;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Reads
{
    public class GetListReadByPivotIdRequestTests
    {
        [Fact]
        public void Properties_SetAndGet()
        {
            // Arrange
            var request = new GetListReadByPivotIdRequest
            {
                PivotId = 7,
                NumberOfReads = 30
            };

            // Act & Assert
            Assert.Equal(7, request.PivotId);
            Assert.Equal(30, request.NumberOfReads);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Arrange
            var request = new GetListReadByPivotIdRequest();

            // Act & Assert
            Assert.Null(request.PivotId);
            Assert.Equal(10, request.NumberOfReads);
        }
    }
}
