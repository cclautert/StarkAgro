namespace AgripeWebAPI.Domain.Commands.Responses.Reads
{
    public class GetReadByPivotIdResponse
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public Quadrante? Quadrante { get; set; }
    }

    public class Quadrante
    {
        public string? TopLeft { get; set; } //Quadrante 4
        public decimal? TopLeftAvg { get; set; } //Quadrante 4
        public string? TopRight { get; set; } //Quadrante 1
        public decimal? TopRightAvg { get; set; } //Quadrante 1
        public string? BottomLeft { get; set; } //Quadrante 3
        public decimal? BottomLeftAvg { get; set; } //Quadrante 3
        public string? BottomRight { get; set; } //Quadrante 2
        public decimal? BottomRightAvg { get; set; } //Quadrante 2
    }
}
