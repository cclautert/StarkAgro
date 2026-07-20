namespace StarkAgroAPI.Domain.Commands.Responses.Users
{
    public enum LoginErrorCode
    {
        None,
        InvalidCredentials,
        AccountInactive,
        TooManyAttempts
    }

    public class UserTokenResponse
    {
        public string? Token { get; set; }
        public LoginErrorCode ErrorCode { get; set; } = LoginErrorCode.None;
    }
}
