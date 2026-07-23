namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Um foco de calor detectado pelo NASA FIRMS dentro (ou até o raio configurado) de uma
    /// <see cref="MonitoredArea"/>. Único por <c>(AreaId, Latitude, Longitude, AcquiredAt,
    /// Satellite)</c> — o índice garante que a reentrega do FIRMS (NRT reprocessa) não vira alerta
    /// duplicado, mesmo sob workers concorrentes. Mesma disciplina de idempotência do NDVI.
    /// </summary>
    public class FireHotspot : Entity
    {
        public int AreaId { get; set; }

        /// <summary>Tenant denormalizado (dono da área) — confirma o isolamento na leitura do sino.</summary>
        public int UserId { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        /// <summary>Momento da passagem do satélite (UTC), de <c>acq_date</c> + <c>acq_time</c>.</summary>
        public DateTime AcquiredAt { get; set; }

        /// <summary>Satélite da detecção (ex.: <c>N</c> = S-NPP, <c>1</c>/<c>N20</c> = NOAA-20).</summary>
        public string Satellite { get; set; } = string.Empty;

        /// <summary>Confiança do FIRMS, crua (<c>low</c>/<c>nominal</c>/<c>high</c> ou <c>l</c>/<c>n</c>/<c>h</c>).
        /// Guardada como veio — nunca gatilha lógica no encoding.</summary>
        public string Confidence { get; set; } = string.Empty;

        /// <summary>Fire Radiative Power (MW) — contexto da intensidade; não é gatilho.</summary>
        public double Frp { get; set; }

        /// <summary>Distância (km) do foco ao centro da área, para o texto do alerta.</summary>
        public double DistanceKm { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
