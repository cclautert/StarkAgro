namespace StarkAgroAPI.Domain.Commands.Responses.Ndvi
{
    public class NdviTrendPoint
    {
        public DateTime AcquisitionDate { get; set; }
        public double NdviMean { get; set; }
        public double NdviMin { get; set; }
        public double NdviMax { get; set; }
        public double CloudCoveragePct { get; set; }
        public bool CloudRejected { get; set; }
    }

    public class NdviTrendResponse
    {
        public int AreaId { get; set; }
        public List<NdviTrendPoint> Points { get; set; } = [];
    }
}
