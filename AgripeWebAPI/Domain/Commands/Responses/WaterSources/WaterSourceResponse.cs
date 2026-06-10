namespace AgripeWebAPI.Domain.Commands.Responses.WaterSources
{
    public class WaterSourceResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<int> PivotIds { get; set; } = new();
        public double MaxFlowLitersPerHour { get; set; }
    }
}
