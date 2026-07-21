using MongoDB.Driver.GeoJsonObjectModel;

namespace StarkAgroAPI.Models.Entities
{
    public static class MonitoredAreaKind
    {
        public const string Circle = "Circle";
        public const string Polygon = "Polygon";
    }

    public static class MonitoredAreaStatus
    {
        public const string Idle = "Idle";
        public const string Queued = "Queued";
        public const string Fetching = "Fetching";
        public const string Failed = "Failed";
    }

    /// <summary>
    /// Área (talhão) que o agricultor monitora por NDVI. A geometria autoritativa é um
    /// <see cref="GeoJsonPolygon{TCoordinates}"/> (ordem <c>[lng, lat]</c>); o círculo guarda
    /// centro+raio só para round-trip da UI. As flags de worker ficam dormentes até a fase de fetch.
    /// </summary>
    public class MonitoredArea : Entity
    {
        /// <summary>Tenant — nunca vem do request.</summary>
        public int UserId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Crop { get; set; }
        public string AreaKind { get; set; } = MonitoredAreaKind.Polygon;

        // Round-trip do círculo (quando AreaKind == Circle)
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public double? RadiusM { get; set; }
        public double? Altitude { get; set; }
        public string? LocationAddress { get; set; }

        /// <summary>Geometria GeoJSON autoritativa (usada em toda chamada à API de NDVI).</summary>
        public GeoJsonPolygon<GeoJson2DGeographicCoordinates> Geometry { get; set; } = default!;

        // ── Flags do worker (dormentes até a fase de fetch) ──
        public bool MonitoringEnabled { get; set; } = true;
        public DateTime? NextFetchAt { get; set; }
        public DateTime? LastFetchAt { get; set; }
        public string? LastAcquisitionDate { get; set; }
        public string Status { get; set; } = MonitoredAreaStatus.Idle;
        public DateTime? ProcessingStartedAt { get; set; }
        public string? WorkerId { get; set; }
        public int RetryCount { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public string? FailureReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
