namespace StarkAgroAPI.Domain.Commands.Responses.Revenda
{
    public class RevendaInviteResponse
    {
        public int Id { get; set; }
        public int RevendaId { get; set; }
        public string? RevendaName { get; set; }
        public string MemberRole { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }
        public DateTime InviteExpiresAt { get; set; }
    }
}
