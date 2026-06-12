namespace AgripeWebAPI.Configuration
{
    public class MongoDbSettings
    {
        public const string SectionName = "MongoDb";

        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;

        // When set, credentials are passed via MongoCredential instead of embedded in the URI.
        // This avoids URI-encoding issues with passwords that contain special characters.
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
