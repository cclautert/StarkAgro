namespace StarkAgroAPI.Notifications
{
    public class Notification(string mensagem)
    {
        public string? Mensagem { get; } = mensagem;
    }
}
