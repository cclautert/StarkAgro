namespace AgripeWebAPI.Models.Entities
{
    public class WaterSource : Entity
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<int> PivotIds { get; set; } = new();
        public double MaxFlowLitersPerHour { get; set; }
    }
}
