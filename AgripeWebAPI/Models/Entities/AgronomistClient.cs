namespace AgripeWebAPI.Models.Entities
{
    public static class AgronomistClientStatus
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Declined = "Declined";
        public const string Revoked = "Revoked";
        public const string Expired = "Expired";
    }

    /// <summary>
    /// Vínculo entre um agrônomo e um produtor da carteira dele.
    /// <para>
    /// É uma coleção, e não um campo <c>User.AgronomistId</c>, porque um campo não suporta
    /// convite/aceite (estado intermediário), não permite convidar quem <b>ainda não tem conta</b>,
    /// e a revogação apagaria a história — que a auditoria de um laudo assinado exige
    /// ("quem era o agrônomo responsável em março?").
    /// </para>
    /// </summary>
    public class AgronomistClient : Entity
    {
        public int AgronomistId { get; set; }

        /// <summary>Nulo enquanto o convidado ainda não tem conta no AgripeWeb.</summary>
        public int? ClientUserId { get; set; }

        public string ClientEmail { get; set; } = string.Empty;

        public string Status { get; set; } = AgronomistClientStatus.Pending;

        public string InviteToken { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }
        public DateTime InviteExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        /// <summary>Quem revogou — o agrônomo ou o próprio produtor (que pode demiti-lo).</summary>
        public int? RevokedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
