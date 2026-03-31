namespace AgripeWebAPI.Domain.Commands.Responses.Pivots
{
    public class GetPivotResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal? LimiteInferior { get; set; }
        public decimal? LimiteSuperior { get; set; }
    }
}
