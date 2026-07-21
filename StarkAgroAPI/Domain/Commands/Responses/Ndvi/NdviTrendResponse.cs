namespace StarkAgroAPI.Domain.Commands.Responses.Ndvi
{
    public class NdviTrendPoint
    {
        public int ReadingId { get; set; }
        public DateTime AcquisitionDate { get; set; }
        public double NdviMean { get; set; }
        public double NdviMin { get; set; }
        public double NdviMax { get; set; }
        public double CloudCoveragePct { get; set; }
        public bool CloudRejected { get; set; }

        /// <summary>ReadingId do overlay quando há PNG gerado (senão null → front não desenha overlay).</summary>
        public int? OverlayReadingId { get; set; }

        /// <summary>Bbox [minLng, minLat, maxLng, maxLat] para alinhar o <c>L.imageOverlay</c>.</summary>
        public double[]? Bbox { get; set; }
    }

    public class NdviTrendResponse
    {
        public int AreaId { get; set; }
        public List<NdviTrendPoint> Points { get; set; } = [];
    }

    public class NdviOverlayImageResponse
    {
        public byte[] Content { get; set; } = [];
        public string ContentType { get; set; } = "image/png";
    }
}
