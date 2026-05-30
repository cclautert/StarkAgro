namespace AgripeWebWorker.Configuration
{
    public class MqttSettings
    {
        public const string SectionName = "Mqtt";
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public string Topic { get; set; } = "reads";
        public string ClientId { get; set; } = "agripeweb-worker";
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
