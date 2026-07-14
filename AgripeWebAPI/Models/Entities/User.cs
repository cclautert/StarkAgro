using System.ComponentModel.DataAnnotations;

namespace AgripeWebAPI.Models.Entities
{
    public class User : Entity
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public bool Active { get; set; }
        public decimal LimiteInferior { get; set; } = 25m;
        public decimal LimiteSuperior { get; set; } = 75m;
        public double? RainThresholdMm { get; set; }
        public string? GeminiApiKey { get; set; }
        public string? ExpoPushToken { get; set; }
        public string? WebPushSubscriptionJson { get; set; }
        public List<string> WebPushSubscriptions { get; set; } = new();
        public int? UplinkIntervalSeconds { get; set; } = 10800;
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Engenheiro agrônomo: revisa e assina os laudos dos clientes vinculados a ele.
        /// <para>
        /// É um bool paralelo ao <see cref="IsAdmin"/> de propósito — reusa toda a plumbing de
        /// claim/guard que já existe. <b>Dívida registrada:</b> quando aparecer o 3º papel,
        /// colapsar os dois booleans em <c>Roles: List&lt;string&gt;</c> de uma vez.
        /// </para>
        /// </summary>
        public bool IsAgronomist { get; set; } = false;

        /// <summary>Registro no CREA, exibido na assinatura do laudo.</summary>
        public string? AgronomistCrea { get; set; }

        public DateTime? AlertsReadAt { get; set; }
    }
}
