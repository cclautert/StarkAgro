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

        /// <summary>
        /// Enable TLS for the MQTT connection. Set Mqtt__Port=8883 when true.
        /// Inject via env var: Mqtt__UseTls=true
        /// </summary>
        public bool UseTls { get; set; } = false;

        /// <summary>
        /// Skip TLS certificate validation. For development only — never true in production.
        /// Inject via env var: Mqtt__AllowUntrustedCertificates=true
        /// </summary>
        public bool AllowUntrustedCertificates { get; set; } = false;
    }
}
