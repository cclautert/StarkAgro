using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace StarkAgroAPI.Models.Entities
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

        /// <summary>
        /// Papéis do usuário (ver <see cref="UserRole"/>). É a fonte da verdade — substitui os
        /// antigos booleans <c>IsAdmin</c>/<c>IsAgronomist</c>. Documentos gravados no formato
        /// antigo são convertidos no boot pela migração idempotente.
        /// </summary>
        public List<string> Roles { get; set; } = new();

        /// <summary>Administrador da plataforma. Computado sobre <see cref="Roles"/>.</summary>
        [BsonIgnore]
        public bool IsAdmin => Roles.Contains(UserRole.Admin);

        /// <summary>
        /// Engenheiro agrônomo: revisa e assina os laudos dos clientes vinculados a ele.
        /// Computado sobre <see cref="Roles"/>.
        /// </summary>
        [BsonIgnore]
        public bool IsAgronomist => Roles.Contains(UserRole.Agronomist);

        /// <summary>Gestor de uma revenda. Computado sobre <see cref="Roles"/>.</summary>
        [BsonIgnore]
        public bool IsResellerManager => Roles.Contains(UserRole.ResellerManager);

        /// <summary>Adiciona ou remove um papel sem duplicar. Não persiste sozinho.</summary>
        public void SetRole(string role, bool enabled)
        {
            if (enabled)
            {
                if (!Roles.Contains(role)) Roles.Add(role);
            }
            else
            {
                Roles.Remove(role);
            }
        }

        /// <summary>Registro no CREA, exibido na assinatura do laudo.</summary>
        public string? AgronomistCrea { get; set; }

        /// <summary>
        /// Quantos laudos este produtor pode enviar por mês.
        /// <para>
        /// <c>null</c> usa o padrão da plataforma; <c>0</c> significa <b>ilimitado</b>. É o que
        /// dá lastro ao plano contratado — e o que impede um único produtor de queimar os
        /// créditos de IA de todo mundo.
        /// </para>
        /// </summary>
        public int? DiagnosisQuotaPerMonth { get; set; }

        /// <summary>
        /// Plano mensal de laudos ao qual o produtor está associado (<see cref="DiagnosisPlan"/>).
        /// <c>null</c> = sem plano (fatura zero; a cota cai no padrão da plataforma).
        /// </summary>
        public int? DiagnosisPlanId { get; set; }

        public DateTime? AlertsReadAt { get; set; }
    }
}
