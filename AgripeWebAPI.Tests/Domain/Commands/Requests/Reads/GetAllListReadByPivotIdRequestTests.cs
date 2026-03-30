using AgripeWebAPI.Domain.Commands.Requests.Reads;

namespace AgripeWebAPI.Tests.Domain.Commands.Requests.Reads
{
    public class GetAllListReadBySensorIdRequestTests
    {
        [Fact]
        public void Properties_SetAndGet()
        {
            // Arrange
            var request = new GetAllListReadBySensorIdRequest
            {
                SensorId = 42,
                Quadrante = 3,
                NumberOfReads = 20
            };

            // Act & Assert
            Assert.Equal(42, request.SensorId);
            Assert.Equal(3, request.Quadrante);
            Assert.Equal(20, request.NumberOfReads);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Arrange
            var request = new GetAllListReadBySensorIdRequest();

            // Act & Assert
            Assert.Null(request.SensorId);
            Assert.Equal(0, request.Quadrante);
            Assert.Equal(10, request.NumberOfReads);
        }
    }
}
