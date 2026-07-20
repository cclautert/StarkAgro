namespace StarkAgroAPI.Domain.Commands.Responses.Reads
{
    public class ReadEntry
    {
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
        public decimal? Humidity { get; set; }
    }

    public class GetReadByPivotIdResponse
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public Quadrante? Quadrante { get; set; }
        public decimal? LimiteInferior { get; set; }
        public decimal? LimiteSuperior { get; set; }
    }

    public class Quadrante
    {
        public string? TopLeft { get; set; } //Quadrante 4
        public decimal? TopLeftAvg { get; set; } //Quadrante 4
        public List<ReadEntry> TopLeftReads { get; set; } = new();

        public string? TopRight { get; set; } //Quadrante 1
        public decimal? TopRightAvg { get; set; } //Quadrante 1
        public List<ReadEntry> TopRightReads { get; set; } = new();

        public string? BottomLeft { get; set; } //Quadrante 3
        public decimal? BottomLeftAvg { get; set; } //Quadrante 3
        public List<ReadEntry> BottomLeftReads { get; set; } = new();

        public string? BottomRight { get; set; } //Quadrante 2
        public decimal? BottomRightAvg { get; set; } //Quadrante 2
        public List<ReadEntry> BottomRightReads { get; set; } = new();
    }
}
