namespace StarkAgroAPI.Domain.Commands.Responses.Ndvi
{
    /// <summary>
    /// Fatia da área numa classe de biomassa. Rótulo, cor e faixa viajam junto com o número
    /// porque o front não pode ter a tabela de cores duplicada — a legenda tem que ser a mesma
    /// que colore o PNG do overlay.
    /// </summary>
    public class NdviClassShare
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public double MinNdvi { get; set; }
        public double MaxNdvi { get; set; }
        public long PixelCount { get; set; }

        /// <summary>Percentual da área válida da passagem (0-100).</summary>
        public double Percent { get; set; }
    }

    public class NdviTrendPoint
    {
        public int ReadingId { get; set; }
        public DateTime AcquisitionDate { get; set; }
        public double NdviMean { get; set; }
        public double NdviMin { get; set; }
        public double NdviMax { get; set; }

        /// <summary>Média de NDRE/NDMI da passagem. Zero em leitura buscada sem índices extras — o
        /// front esconde essas séries quando nenhum ponto tem valor, para não desenhar linha reta.</summary>
        public double NdreMean { get; set; }
        public double NdmiMean { get; set; }

        public double CloudCoveragePct { get; set; }
        public bool CloudRejected { get; set; }

        /// <summary>
        /// Distribuição por nível de biomassa. Vazia em passagem nublada ou gravada antes da
        /// classificação existir — o front esconde o painel nesse caso.
        /// </summary>
        public List<NdviClassShare> Classes { get; set; } = [];

        /// <summary>ReadingId do overlay quando há PNG gerado (senão null → front não desenha overlay).</summary>
        public int? OverlayReadingId { get; set; }

        /// <summary>Bbox [minLng, minLat, maxLng, maxLat] para alinhar o <c>L.imageOverlay</c>.</summary>
        public double[]? Bbox { get; set; }
    }

    /// <summary>
    /// Ponto da série de radar (Sentinel-1). Datas próprias — o S1 tem cadência e passagens
    /// diferentes do S2, então NÃO casa com os pontos do NDVI; é uma série à parte.
    /// </summary>
    public class Sentinel1TrendPoint
    {
        public DateTime AcquisitionDate { get; set; }
        public double RviMean { get; set; }
        public double VvMean { get; set; }
        public double VhMean { get; set; }
    }

    public class NdviTrendResponse
    {
        public int AreaId { get; set; }
        public List<NdviTrendPoint> Points { get; set; } = [];

        /// <summary>Série de radar (RVI). Vazia se o S1 nunca rodou para a área.</summary>
        public List<Sentinel1TrendPoint> Radar { get; set; } = [];
    }

    public class NdviOverlayImageResponse
    {
        public byte[] Content { get; set; } = [];
        public string ContentType { get; set; } = "image/png";
    }
}
