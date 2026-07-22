using MongoDB.Bson;

namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Pixels de uma classe de biomassa nesta passagem. <c>Key</c> casa com
    /// <c>NdviClassification.Classes[].Key</c> — é chave estável, não índice: reordenar as
    /// classes não pode reinterpretar leitura já gravada.
    /// </summary>
    public class NdviClassCount
    {
        public string Key { get; set; } = string.Empty;
        public long PixelCount { get; set; }
    }

    /// <summary>
    /// Uma passagem de NDVI de uma <see cref="MonitoredArea"/> (uma aquisição do Sentinel-2).
    /// Único por <c>(AreaId, AcquisitionDate)</c> — o índice garante que a mesma passagem não é
    /// gravada (nem paga) duas vezes, mesmo sob workers concorrentes.
    /// </summary>
    public class NdviReading : Entity
    {
        public int AreaId { get; set; }

        /// <summary>Tenant denormalizado (dono da área) — confirma o isolamento na leitura.</summary>
        public int UserId { get; set; }

        /// <summary>Data da aquisição do Sentinel-2 (UTC, verbatim da CDSE).</summary>
        public DateTime AcquisitionDate { get; set; }

        public double NdviMean { get; set; }
        public double NdviMin { get; set; }
        public double NdviMax { get; set; }
        public double NdviStdev { get; set; }

        /// <summary>Cobertura de nuvem da passagem, em %.</summary>
        public double CloudCoveragePct { get; set; }

        /// <summary>
        /// Distribuição da área entre as classes de biomassa (histograma da Statistical API).
        /// Passagem nublada e documento gravado antes desta feature ficam com a lista vazia —
        /// não há migração, as classes aparecem a partir da próxima passagem do satélite.
        /// </summary>
        public List<NdviClassCount> ClassCounts { get; set; } = [];

        /// <summary>Passagem nublada demais: guardada como buraco honesto na série, não como falha.</summary>
        public bool CloudRejected { get; set; }

        /// <summary>Overlay PNG no GridFS (fase posterior). Nulo até lá.</summary>
        public ObjectId? OverlayImageFileId { get; set; }

        /// <summary>Custo desta busca (Processing Units), em centavos, congelado no momento.</summary>
        public int NdviCostCents { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
