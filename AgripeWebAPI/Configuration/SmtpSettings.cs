namespace AgripeWebAPI.Configuration
{
    public class SmtpSettings
    {
        public const string SectionName = "Smtp";

        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string FromName { get; set; } = "AgripeWeb";
        public string? FromEmail { get; set; }
        public bool UseStartTls { get; set; } = true;

        /// <summary>
        /// Sem host ou remetente configurado, o serviço de e-mail apenas registra em log e não
        /// tenta enviar — o alerta e o laudo continuam funcionando pelo push e pela tela.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host)
            && !string.IsNullOrWhiteSpace(FromEmail)
            && Host != "CHANGE_ME";
    }
}
