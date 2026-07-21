namespace StarkAgroAPI.Domain.Commands.Responses.Revenda
{
    public class RevendaMemberResponse
    {
        public int Id { get; set; }
        public int? MemberUserId { get; set; }
        public string MemberEmail { get; set; } = string.Empty;
        public string? MemberName { get; set; }
        public string MemberRole { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }
        public DateTime InviteExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }
}
