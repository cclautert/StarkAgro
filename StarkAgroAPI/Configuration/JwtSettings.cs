namespace StarkAgroAPI.Configuration
{
    public class JwtSettings
    {
        public string? secretkey { get; set; }
        public string? issuer { get; set; }
        public string? audience { get; set; }
    }
}
