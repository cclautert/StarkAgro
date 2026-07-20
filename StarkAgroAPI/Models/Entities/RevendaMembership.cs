namespace StarkAgroAPI.Models.Entities
{
    /// <summary>Papel de um membro dentro da revenda.</summary>
    public static class RevendaMemberRole
    {
        public const string Manager = "Manager";
        public const string Agronomist = "Agronomist";
        public const string Client = "Client";
    }

    /// <summary>Ciclo de vida de um vínculo de revenda. Mesmo conjunto de <see cref="AgronomistClientStatus"/>.</summary>
    public static class RevendaMembershipStatus
    {
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Declined = "Declined";
        public const string Revoked = "Revoked";
        public const string Expired = "Expired";
    }

    /// <summary>
    /// Vínculo entre uma revenda e um membro (gestor, agrônomo ou cliente produtor).
    /// <para>
    /// É uma coleção, e não um campo em <see cref="User"/>, pelas mesmas razões de
    /// <see cref="AgronomistClient"/>: suporta convite/aceite (estado intermediário), permite
    /// convidar quem ainda não tem conta e preserva a história quando um membro sai.
    /// </para>
    /// </summary>
    public class RevendaMembership : Entity
    {
        public int RevendaId { get; set; }

        public string MemberRole { get; set; } = RevendaMemberRole.Client;

        /// <summary>Nulo enquanto o convidado ainda não tem conta no StarkAgro.</summary>
        public int? MemberUserId { get; set; }

        public string MemberEmail { get; set; } = string.Empty;

        public string Status { get; set; } = RevendaMembershipStatus.Pending;

        public string InviteToken { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }
        public DateTime InviteExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        /// <summary>Quem revogou — a revenda ou o próprio membro.</summary>
        public int? RevokedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
