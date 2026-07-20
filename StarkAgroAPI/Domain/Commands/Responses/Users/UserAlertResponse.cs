namespace StarkAgroAPI.Domain.Commands.Responses.Users
{
    public class UserAlertResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PivotName { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}
