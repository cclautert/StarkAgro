namespace AgripeWebAPI.Configuration
{
    public class MqttDownlinkSettings
    {
        public const string SectionName = "MqttDownlink";
        public string Host     { get; set; } = "localhost";
        public int    Port     { get; set; } = 8883;
        public string Topic    { get; set; } = "writes";
        public string ClientId { get; set; } = "agripeweb-api";
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool   UseTls   { get; set; } = true;
        public bool   AllowUntrustedCertificates { get; set; } = false;
    }
}
