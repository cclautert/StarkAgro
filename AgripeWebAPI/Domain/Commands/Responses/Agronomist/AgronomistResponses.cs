namespace AgripeWebAPI.Domain.Commands.Responses.Agronomist
{
    public class AgronomistQueueItemResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ClientUserId { get; set; }
        public string? ClientName { get; set; }
        public string? PivotName { get; set; }
        public string? CropName { get; set; }
        public string? TopDisease { get; set; }
        public double TopProbability { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewStartedAt { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class AgronomistClientResponse
    {
        public int Id { get; set; }
        public int? ClientUserId { get; set; }
        public string ClientEmail { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }
        public DateTime InviteExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public int PendingDiagnoses { get; set; }
    }

    public class AgronomistInviteResponse
    {
        public int Id { get; set; }
        public int AgronomistId { get; set; }
        public string? AgronomistName { get; set; }
        public string? AgronomistCrea { get; set; }
        public DateTime InvitedAt { get; set; }
        public DateTime InviteExpiresAt { get; set; }
    }
}
