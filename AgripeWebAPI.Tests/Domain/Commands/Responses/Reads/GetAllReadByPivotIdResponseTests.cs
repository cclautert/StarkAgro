using AgripeWebAPI.Domain.Commands.Responses.Reads;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Reads
{
    public class GetAllReadByPivotIdResponseTests
    {
        [Fact]
        public void Properties_SetAndGet()
        {
            // Arrange
            var date = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var response = new GetAllReadByPivotIdResponse
            {
                Id = 100,
                SensorId = 5,
                Value = 42.75m,
                Date = date
            };

            // Act & Assert
            Assert.Equal(100, response.Id);
            Assert.Equal(5, response.SensorId);
            Assert.Equal(42.75m, response.Value);
            Assert.Equal(date, response.Date);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Arrange
            var response = new GetAllReadByPivotIdResponse();

            // Act & Assert
            Assert.Equal(0, response.Id);
            Assert.Equal(0, response.SensorId);
            Assert.Equal(0m, response.Value);
            Assert.Equal(default(DateTime), response.Date);
        }
    }
}
