namespace StarkAgroAPI.Configuration
{
    public class OAuthSettings
    {
        public const string SectionName = "OAuth";

        public GoogleOAuthSettings Google { get; set; } = new();
    }

    public class GoogleOAuthSettings
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        /// <summary>
        /// Comma-separated list of allowed redirect URIs (e.g. https://localhost:4200/login/callback).
        /// Must match the URI registered in Google Cloud Console.
        /// </summary>
        public string? AllowedRedirectUris { get; set; }
    }
}
