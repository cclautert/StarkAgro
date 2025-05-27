namespace AgripeWebAPI.Notifications
{
    public class Notification(string mensagem)
    {
        public string? Mensagem { get; } = mensagem;
    }
}
