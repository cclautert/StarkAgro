namespace AgripeWebAPI.Domain.Commands.Responses.Users
{
    public class UserTokenResponse
    {
        public string Token { get; set; }

        public DateTime Expiration { get; set; }
    }
}
