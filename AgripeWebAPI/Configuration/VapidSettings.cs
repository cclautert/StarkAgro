namespace AgripeWebAPI.Configuration
{
    public class VapidSettings
    {
        public const string SectionName = "Vapid";

        public string Subject { get; set; } = "CHANGE_ME";
        public string PublicKey { get; set; } = "CHANGE_ME";
        public string PrivateKey { get; set; } = "CHANGE_ME";
    }
}
