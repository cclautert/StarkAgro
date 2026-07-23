namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Uma passagem de radar (Sentinel-1 GRD) de uma <see cref="MonitoredArea"/>. Separada de
    /// <see cref="NdviReading"/> de propósito — dado de outra missão, outra cadência. Único por
    /// <c>(AreaId, AcquisitionDate, OrbitDirection)</c>: a órbita entra na chave porque a série
    /// nunca mistura ascendente e descendente (backscatter pularia por geometria de visada). Mesma
    /// disciplina de idempotência do NDVI.
    /// </summary>
    public class Sentinel1Reading : Entity
    {
        public int AreaId { get; set; }

        /// <summary>Tenant denormalizado (dono da área) — confirma o isolamento na leitura.</summary>
        public int UserId { get; set; }

        public DateTime AcquisitionDate { get; set; }

        /// <summary>Órbita da passagem (<c>DESCENDING</c> fixo). Parte da chave de dedup.</summary>
        public string OrbitDirection { get; set; } = string.Empty;

        /// <summary>RVI = 4·VH/(VV+VH) — proxy de biomassa/estrutura por radar.</summary>
        public double RviMean { get; set; }

        /// <summary>Backscatter cru (GAMMA0 linear) — contexto.</summary>
        public double VvMean { get; set; }
        public double VhMean { get; set; }

        /// <summary>Custo desta busca (Processing Units), em centavos, congelado no momento.</summary>
        public int Sentinel1CostCents { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
