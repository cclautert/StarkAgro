using AgripeWebAPI.Domain.Commands.Responses.Reads;

namespace AgripeWebAPI.Tests.Domain.Commands.Responses.Reads
{
    public class GetReadByPivotIdResponseTests
    {
        [Fact]
        public void Properties_SetAndGet()
        {
            // Arrange
            var quadrante = new Quadrante
            {
                TopLeft = "#2196F3",
                TopLeftAvg = 10.5m,
                TopRight = "#4CAF50",
                TopRightAvg = 35.0m,
                BottomLeft = "#FFC107",
                BottomLeftAvg = 55.0m,
                BottomRight = "#F44336",
                BottomRightAvg = 80.0m
            };

            var response = new GetReadByPivotIdResponse
            {
                Id = 5,
                Name = "Pivot A",
                Quadrante = quadrante
            };

            // Act & Assert
            Assert.Equal(5, response.Id);
            Assert.Equal("Pivot A", response.Name);
            Assert.NotNull(response.Quadrante);
            Assert.Equal("#2196F3", response.Quadrante.TopLeft);
            Assert.Equal(10.5m, response.Quadrante.TopLeftAvg);
            Assert.Equal("#4CAF50", response.Quadrante.TopRight);
            Assert.Equal(35.0m, response.Quadrante.TopRightAvg);
            Assert.Equal("#FFC107", response.Quadrante.BottomLeft);
            Assert.Equal(55.0m, response.Quadrante.BottomLeftAvg);
            Assert.Equal("#F44336", response.Quadrante.BottomRight);
            Assert.Equal(80.0m, response.Quadrante.BottomRightAvg);
        }

        [Fact]
        public void DefaultValues_AreNull()
        {
            // Arrange
            var response = new GetReadByPivotIdResponse();

            // Act & Assert
            Assert.Null(response.Id);
            Assert.Null(response.Name);
            Assert.Null(response.Quadrante);
        }

        [Fact]
        public void Quadrante_DefaultValues_AreNull()
        {
            // Arrange
            var quadrante = new Quadrante();

            // Act & Assert
            Assert.Null(quadrante.TopLeft);
            Assert.Null(quadrante.TopLeftAvg);
            Assert.Null(quadrante.TopRight);
            Assert.Null(quadrante.TopRightAvg);
            Assert.Null(quadrante.BottomLeft);
            Assert.Null(quadrante.BottomLeftAvg);
            Assert.Null(quadrante.BottomRight);
            Assert.Null(quadrante.BottomRightAvg);
        }

        [Fact]
        public void Quadrante_ReadLists_DefaultEmpty()
        {
            var q = new Quadrante();
            Assert.NotNull(q.TopLeftReads);
            Assert.NotNull(q.TopRightReads);
            Assert.NotNull(q.BottomLeftReads);
            Assert.NotNull(q.BottomRightReads);
            Assert.Empty(q.TopLeftReads);
            Assert.Empty(q.TopRightReads);
            Assert.Empty(q.BottomLeftReads);
            Assert.Empty(q.BottomRightReads);
        }

        [Fact]
        public void ReadEntry_SetAndGet()
        {
            var date = DateTime.UtcNow;
            var entry = new ReadEntry { Value = 42.5m, Date = date };
            Assert.Equal(42.5m, entry.Value);
            Assert.Equal(date, entry.Date);
        }

        [Fact]
        public void GetReadByPivotIdResponse_LimiteInferior_LimiteSuperior()
        {
            var response = new GetReadByPivotIdResponse { LimiteInferior = 10m, LimiteSuperior = 90m };
            Assert.Equal(10m, response.LimiteInferior);
            Assert.Equal(90m, response.LimiteSuperior);
        }
    }
}
