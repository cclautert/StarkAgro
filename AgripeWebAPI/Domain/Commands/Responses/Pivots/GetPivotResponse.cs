namespace AgripeWebAPI.Domain.Commands.Responses.Pivots
{
    public class GetPivotResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? LimiteInferior { get; set; }
        public decimal? LimiteSuperior { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Altitude { get; set; }
        public string? LocationAddress { get; set; }
        public DateTime? LocationUpdatedAt { get; set; }
    }
}
