namespace AccountManager.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AccountManager";
    public string Audience { get; set; } = "AccountManager.Api";
    public string SigningKey { get; set; } = "CHANGE_ME_TO_A_LONG_SECRET_KEY_32+";
    public int ExpirationMinutes { get; set; } = 60;
}
