using StarkAgroAPI.Models;

namespace StarkAgroAPI.Domain.Commands.Responses.Ndvi
{
    public class MonitoredAreaResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Crop { get; set; }
        public string AreaKind { get; set; } = string.Empty;
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public double? RadiusM { get; set; }
        public double? Altitude { get; set; }
        public string? LocationAddress { get; set; }
        public bool MonitoringEnabled { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<GeoCoordinate> Ring { get; set; } = [];
        public DateTime? LastFetchAt { get; set; }
        public string? LastAcquisitionDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
