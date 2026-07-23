namespace StarkAgroAPI.Models.Entities
{
    public static class ClimateAlertType
    {
        public const string Frost = "FrostRisk";
        public const string Heat = "HeatRisk";
    }

    /// <summary>
    /// Alerta de risco climático (geada ou calor extremo) para uma <see cref="MonitoredArea"/> num
    /// dia previsto. Único por <c>(AreaId, AlertType, ForecastDate)</c> — a previsão é reavaliada a
    /// cada tick e o índice impede que o mesmo risco vire alerta/push repetido. Mesmo padrão de
    /// idempotência do NDVI e dos focos de calor.
    /// </summary>
    public class ClimateAlert : Entity
    {
        public int AreaId { get; set; }

        /// <summary>Tenant denormalizado (dono da área) — confirma o isolamento na leitura do sino.</summary>
        public int UserId { get; set; }

        /// <summary><see cref="ClimateAlertType.Frost"/> ou <see cref="ClimateAlertType.Heat"/>.</summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>Dia previsto do risco (UTC, sem hora) — parte da chave de dedup.</summary>
        public DateTime ForecastDate { get; set; }

        /// <summary>Temperatura prevista que cruzou o limiar (°C): mín na geada, máx no calor.</summary>
        public double TemperatureC { get; set; }

        /// <summary>Limiar configurado no momento do alerta (°C) — para o texto e auditoria.</summary>
        public double ThresholdC { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
